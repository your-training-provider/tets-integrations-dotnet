using System.Runtime.CompilerServices;

// Grants the test project access to internal types (e.g. ApiConnection) that are unit-tested
// directly, alongside the public-surface behavioral tests that go through TetsIntegrationsClient.
[assembly: InternalsVisibleTo("TeTS.Integrations.Tests")]
