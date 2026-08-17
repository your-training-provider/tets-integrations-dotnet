using System.Text.Json.Serialization;
using TeTS.Integrations.Models;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace TeTS.Integrations.Tests;

/// <summary>
/// Locks the SDK to contract/integrations-v1.yaml: every operation must be known,
/// and every required schema property must exist on the mapped model with the right wire name.
/// A failure here means the server contract changed — update the SDK before partners hit the gap.
/// </summary>
public class ContractParityTests
{
    private static YamlMappingNode LoadRoot()
    {
        using var reader = new StreamReader(Path.Combine(AppContext.BaseDirectory, "contract/integrations-v1.yaml"));
        var yaml = new YamlStream();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    // Adding an endpoint to the API contract? Wrap it in the SDK and add it here.
    private static readonly HashSet<string> WrappedOperations = new()
    {
        "get /api/integrations/v1/openapi.yaml",   // docs download — intentionally not wrapped
        "get /api/integrations/v1/ping",
        "post /api/integrations/v1/users",
        "get /api/integrations/v1/users",
        "patch /api/integrations/v1/users",
        "get /api/integrations/v1/users/exists",
        "patch /api/integrations/v1/users/status",
        "get /api/integrations/v1/users/list",
        "get /api/integrations/v1/reports/completions",
        "get /api/integrations/v1/catalog",
        "get /api/integrations/v1/sso",            // browser redirect — covered by SsoUrlBuilder
    };

    private static readonly Dictionary<string, Type> SchemaToModel = new()
    {
        ["PingResponse"] = typeof(PingResponse),
        ["CreateUserRequest"] = typeof(CreateUserRequest),
        ["CreateUserResult"] = typeof(CreateUserResult),
        ["User"] = typeof(User),
        ["UpdateUserRequest"] = typeof(UpdateUserRequest),
        ["UserExistsResponse"] = typeof(UserExistsResponse),
        ["UpdateUserStatusRequest"] = typeof(UserStatusChangeRequest),
        ["UpdateUserStatusResult"] = typeof(UserStatusResult),
        ["UserListItem"] = typeof(UserListItem),
        ["UserListResponse"] = typeof(UserListResponse),   // internal envelope, visible via InternalsVisibleTo
        ["Pagination"] = typeof(Pagination),
        ["CompletionRecord"] = typeof(CompletionRecord),
        ["CompletionsReport"] = typeof(CompletionsReport),
        ["CatalogProgramCourse"] = typeof(CatalogProgramCourse),
        ["CatalogItem"] = typeof(CatalogItem),
        ["CatalogListResponse"] = typeof(CatalogListResponse),   // internal envelope, visible via InternalsVisibleTo
        ["ErrorDetail"] = typeof(ErrorDetail),
        // Error: consumed internally, surfaced via TetsApiException — checked by name below
    };

    [Fact]
    public void EveryContractOperationIsAccountedFor()
    {
        var paths = (YamlMappingNode)LoadRoot().Children[new YamlScalarNode("paths")];
        var unaccounted = new List<string>();
        foreach (var path in paths.Children)
            foreach (var method in ((YamlMappingNode)path.Value).Children)
            {
                var op = $"{((YamlScalarNode)method.Key).Value} {((YamlScalarNode)path.Key).Value}";
                if (!WrappedOperations.Contains(op)) unaccounted.Add(op);
            }
        Assert.True(unaccounted.Count == 0,
            $"Contract has operations this SDK does not wrap: {string.Join(", ", unaccounted)}");
    }

    [Fact]
    public void EveryRequiredSchemaPropertyExistsOnItsModel()
    {
        var schemas = (YamlMappingNode)((YamlMappingNode)LoadRoot()
            .Children[new YamlScalarNode("components")]).Children[new YamlScalarNode("schemas")];
        var problems = new List<string>();

        foreach (var (schemaName, model) in SchemaToModel)
        {
            var schema = (YamlMappingNode)schemas.Children[new YamlScalarNode(schemaName)];
            if (!schema.Children.TryGetValue(new YamlScalarNode("properties"), out var propsNode)) continue;
            var wireNames = model.GetProperties()
                .Select(p => p.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
                    .Cast<JsonPropertyNameAttribute>().FirstOrDefault()?.Name)
                .Where(n => n is not null).ToHashSet();

            // ALL contract properties (not just required) must exist on the model.
            foreach (var prop in ((YamlMappingNode)propsNode).Children)
            {
                var name = ((YamlScalarNode)prop.Key).Value!;
                if (!wireNames.Contains(name))
                    problems.Add($"{schemaName}.{name} missing on {model.Name}");
            }
        }
        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    [Fact]
    public void ErrorSchemaCodesMatchEnum()
    {
        var schemas = (YamlMappingNode)((YamlMappingNode)LoadRoot()
            .Children[new YamlScalarNode("components")]).Children[new YamlScalarNode("schemas")];
        var error = (YamlMappingNode)schemas.Children[new YamlScalarNode("Error")];
        var props = (YamlMappingNode)error.Children[new YamlScalarNode("properties")];
        var codeNode = (YamlMappingNode)props.Children[new YamlScalarNode("code")];
        var enumNode = (YamlSequenceNode)codeNode.Children[new YamlScalarNode("enum")];
        foreach (var code in enumNode.Children.Cast<YamlScalarNode>())
        {
            var mapped = TeTS.Integrations.TetsErrorCodeMapper.Map(code.Value);
            Assert.True(mapped != TeTS.Integrations.TetsErrorCode.Unknown,
                $"Contract error code '{code.Value}' has no TetsErrorCode mapping in TetsErrorCodeMapper.");
        }
    }
}
