using System.Web;
using TeTS.Integrations;
using TeTS.Integrations.Sso;
using Xunit;

namespace TeTS.Integrations.Tests;

public class SsoTests
{
    // Vectors generated from lib/auth/integrationSso.ts (Unified-App) on 2026-08-14.
    [Theory]
    [InlineData("test-secret", "casey.lee", "28800", "1783332000", "63d7f0a4afedbc795496be859a186c9f")]
    [InlineData("partner-staging-secret", "user with spaces", "28800", "1786500000", "cc00ef52963116b1e2aefb17ce0684ff")]
    public void SignatureMatchesServerImplementation(string secret, string username, string timeout, string ts, string expected)
        => Assert.Equal(expected, SsoUrlBuilder.ComputeSignature(secret, username, timeout, ts));

    [Fact]
    public void NonAsciiInput_ThrowsInsteadOfDiverging()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SsoUrlBuilder.ComputeSignature("secret", "maría.gonzález", "28800", "1783332000"));
        // Non-secret inputs may safely echo the offending character and name the field.
        Assert.Contains("username", ex.Message);
        Assert.Contains("í", ex.Message);
    }

    [Fact]
    public void ComputeSignature_NonAsciiSecret_ThrowsWithoutEchoingCharacter()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SsoUrlBuilder.ComputeSignature("sëcret", "casey.lee", "28800", "1783332000"));
        Assert.DoesNotContain("ë", ex.Message);
    }

    [Fact]
    public void Constructor_NonAsciiSecret_ThrowsWithoutEchoingCharacter()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new SsoUrlBuilder("https://courses.example.com", "acme", "sëcret"));
        Assert.DoesNotContain("ë", ex.Message);
        Assert.Equal("ssoSecret", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidBaseUrl_ThrowsNamingBaseUrl()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SsoUrlBuilder("not-a-url", "acme", "secret"));
        Assert.Equal("baseUrl", ex.ParamName);
    }

    [Theory]
    [InlineData("https://h.example?x=1")]
    [InlineData("https://h.example#frag")]
    public void Constructor_BaseUrlWithQueryOrFragment_ThrowsNamingBaseUrl(string baseUrl)
    {
        var ex = Assert.Throws<ArgumentException>(() => new SsoUrlBuilder(baseUrl, "acme", "secret"));
        Assert.Equal("baseUrl", ex.ParamName);
    }

    [Fact]
    public void ComputeSignature_Md5Unavailable_ThrowsActionableInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SsoUrlBuilder.ComputeSignatureCore("secret", "u", "28800", "1783332000",
                () => throw new InvalidOperationException("FIPS policy violation")));
        Assert.Contains("MD5", ex.Message);
        Assert.Contains("FIPS", ex.Message);
    }

    private static SsoUrlBuilder Builder() => new("https://courses.example.com/", "acme", "test-secret");

    [Fact]
    public void BuildLaunchUrl_NullRequest_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Builder().BuildLaunchUrl(null!));
        Assert.Equal("request", ex.ParamName);
    }

    [Fact]
    public void BuildLaunchUrl_MinimalRequest()
    {
        var url = Builder().BuildLaunchUrl(new SsoLaunchRequest
        { UserName = "casey.lee", TimestampOverride = 1783332000 }).ToString();
        Assert.StartsWith("https://courses.example.com/api/integrations/v1/sso?", url);
        var q = HttpUtility.ParseQueryString(new Uri(url).Query);
        Assert.Equal("acme", q["integration"]);
        Assert.Equal("casey.lee", q["username"]);
        Assert.Equal("1783332000", q["timestamp"]);
        Assert.Equal("28800", q["sessionTimeOut"]);  // default = 8h max
        Assert.Equal("63d7f0a4afedbc795496be859a186c9f", q["signature"]);
        Assert.Null(q["courseId"]);                  // unset optionals omitted
        Assert.Null(q["embed"]);
    }

    [Fact]
    public void BuildLaunchUrl_FullTopyxParitySurface()
    {
        var url = Builder().BuildLaunchUrl(new SsoLaunchRequest
        {
            UserName = "casey.lee", TimestampOverride = 1783332000, SessionTimeOutSeconds = 3600,
            Identification = "staff-guid-1", FirstName = "Casey", LastName = "Lee",
            Email = "c@example.com", Organization = "Acme Care", JobTitle = "RN",
            CourseId = "42", CourseName = "Fire Safety", ContentId = "77",
            ProgramId = "9", ProgramName = "Onboarding",
            OrganizationTenantId = "tenant-1", Embed = true, EmbedOrigin = "https://app.acme.example",
        }).ToString();
        var q = HttpUtility.ParseQueryString(new Uri(url).Query);
        Assert.Equal("3600", q["sessionTimeOut"]);
        Assert.Equal("staff-guid-1", q["identification"]);
        Assert.Equal("Casey", q["firstName"]);
        Assert.Equal("Acme Care", q["organization"]);
        Assert.Equal("RN", q["jobTitle"]);
        Assert.Equal("42", q["courseId"]);
        Assert.Equal("Fire Safety", q["courseName"]);
        Assert.Equal("77", q["contentId"]);
        Assert.Equal("9", q["programId"]);
        Assert.Equal("Onboarding", q["programName"]);
        Assert.Equal("tenant-1", q["organizationTenantId"]);
        Assert.Equal("1", q["embed"]);
        Assert.Equal("https://app.acme.example", q["embedOrigin"]);
        // signature covers sessionTimeOut=3600
        Assert.Equal(SsoUrlBuilder.ComputeSignature("test-secret", "casey.lee", "3600", "1783332000"), q["signature"]);
    }

    [Fact]
    public void UserName_TrimmedBeforeSigningAndEmission()
    {
        var url = Builder().BuildLaunchUrl(new SsoLaunchRequest
        { UserName = "casey.lee ", TimestampOverride = 1783332000 }).ToString();
        var q = HttpUtility.ParseQueryString(new Uri(url).Query);
        Assert.Equal("casey.lee", q["username"]);
        Assert.Equal("63d7f0a4afedbc795496be859a186c9f", q["signature"]);
    }

    [Fact]
    public void BuildLaunchUrl_SignatureCoversExactlyTheEmittedValues()
    {
        // No TimestampOverride: exercises the live-timestamp path. Decode the emitted values back
        // out of the query string and recompute — this kills both "signed the escaped value" and
        // "signed a different timestamp than the one emitted" mutations.
        var url = Builder().BuildLaunchUrl(new SsoLaunchRequest { UserName = "user with spaces" }).ToString();
        var q = HttpUtility.ParseQueryString(new Uri(url).Query);
        var recomputed = SsoUrlBuilder.ComputeSignature("test-secret", q["username"]!, q["sessionTimeOut"]!, q["timestamp"]!);
        Assert.Equal(q["signature"], recomputed);
    }

    [Fact]
    public void BuildLaunchUrl_EmptyStringOptionalParam_Omitted()
    {
        var url = Builder().BuildLaunchUrl(new SsoLaunchRequest
        { UserName = "u", FirstName = "", TimestampOverride = 1783332000 }).ToString();
        Assert.DoesNotContain("firstName=", url);
    }

    [Fact]
    public void TimestampOverride_NotPositive_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Builder().BuildLaunchUrl(
            new SsoLaunchRequest { UserName = "u", TimestampOverride = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Builder().BuildLaunchUrl(
            new SsoLaunchRequest { UserName = "u", TimestampOverride = -1 }));
    }

    [Fact]
    public void Embed_True_WithoutEmbedOrigin_ThrowsNamingEmbedOrigin()
    {
        var ex = Assert.Throws<ArgumentException>(() => Builder().BuildLaunchUrl(
            new SsoLaunchRequest { UserName = "u", Embed = true }));
        Assert.Contains("EmbedOrigin", ex.Message);
    }

    [Fact]
    public void EmbedOrigin_WithoutEmbed_ThrowsTellingCallerToSetEmbed()
    {
        var ex = Assert.Throws<ArgumentException>(() => Builder().BuildLaunchUrl(
            new SsoLaunchRequest { UserName = "u", Embed = false, EmbedOrigin = "https://app.partner.example" }));
        Assert.Contains("Embed = true", ex.Message);
    }

    [Theory]
    [InlineData("app.partner.example")]                 // not absolute
    [InlineData("ftp://app.partner.example")]            // wrong scheme
    [InlineData("http://app.partner.example")]           // http on non-loopback host
    [InlineData("https://app.partner.example/launch")]   // path beyond "/"
    [InlineData("https://app.partner.example?x=1")]      // query string
    [InlineData("https://app.partner.example#frag")]     // fragment
    [InlineData("https://user@app.example")]             // userinfo — server rejects userinfo origins
    public void EmbedOrigin_InvalidShape_ThrowsShowingExpectedShape(string origin)
    {
        var ex = Assert.Throws<ArgumentException>(() => Builder().BuildLaunchUrl(
            new SsoLaunchRequest { UserName = "u", Embed = true, EmbedOrigin = origin }));
        Assert.Contains("EmbedOrigin", ex.Message);
        Assert.Contains("https://app.partner.example", ex.Message);
        // ParamName must name the public BuildLaunchUrl parameter ("request"), not the private
        // helper's internal parameter name ("origin"), which is invisible to callers.
        Assert.Equal("request", ex.ParamName);
    }

    [Fact]
    public void EmbedOrigin_ValidHttps_Passes()
    {
        var url = Builder().BuildLaunchUrl(new SsoLaunchRequest
        {
            UserName = "u", Embed = true, EmbedOrigin = "https://app.partner.example",
            TimestampOverride = 1783332000,
        }).ToString();
        var q = HttpUtility.ParseQueryString(new Uri(url).Query);
        Assert.Equal("1", q["embed"]);
        Assert.Equal("https://app.partner.example", q["embedOrigin"]);
    }

    [Fact]
    public void EmbedOrigin_ValidHttpLocalhost_Passes()
    {
        var url = Builder().BuildLaunchUrl(new SsoLaunchRequest
        {
            UserName = "u", Embed = true, EmbedOrigin = "http://localhost:3000",
            TimestampOverride = 1783332000,
        }).ToString();
        var q = HttpUtility.ParseQueryString(new Uri(url).Query);
        Assert.Equal("http://localhost:3000", q["embedOrigin"]);
    }

    [Fact]
    public void BuildLaunchUrl_StampsCurrentTimestamp_WhenNoOverride()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var q = HttpUtility.ParseQueryString(
            new Uri(Builder().BuildLaunchUrl(new SsoLaunchRequest { UserName = "u" }).ToString()).Query);
        var ts = long.Parse(q["timestamp"]!);
        Assert.InRange(ts, before, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    [Fact]
    public void SessionTimeout_OutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Builder().BuildLaunchUrl(
            new SsoLaunchRequest { UserName = "u", SessionTimeOutSeconds = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Builder().BuildLaunchUrl(
            new SsoLaunchRequest { UserName = "u", SessionTimeOutSeconds = 28801 }));
    }

    [Fact]
    public void ClientSsoProperty_RequiresSecretAndSlug()
    {
        var bare = new TetsIntegrationsClient(new HttpClient(new TestHttpHandler()),
            new TetsOptions { BaseUrl = "https://x.example.com", ApiKey = "k" });
        Assert.Throws<InvalidOperationException>(() => bare.Sso);

        var configured = new TetsIntegrationsClient(new HttpClient(new TestHttpHandler()),
            new TetsOptions { BaseUrl = "https://x.example.com", ApiKey = "k", IntegrationSlug = "acme", SsoSecret = "s" });
        Assert.NotNull(configured.Sso);
    }
}
