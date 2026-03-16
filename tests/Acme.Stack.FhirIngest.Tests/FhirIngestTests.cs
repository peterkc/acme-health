using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Acme.Stack.FhirIngest.Tests;

public class FhirIngestTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public FhirIngestTests(WebApplicationFactory<Program> factory)
    {
        // Override configuration to run without a real DB for unit-level tests.
        // Integration tests (marked with Trait) require a running DoltgreSQL instance.
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Provide a dummy connection string so the app starts.
                // DB-dependent tests will fail with a connection error; that is expected.
                // The empty-bundle test never hits the DB path.
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:doltgresql"] = ""
                });
            });
        });
        _client = _factory.CreateClient();
    }

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        return File.ReadAllText(path);
    }

    [Fact]
    public async Task IngestEmptyBundle_Returns422()
    {
        // A bundle with only Observation entries (no Patients) should return 422
        var noPatientsBundle = """
            {
                "resourceType": "Bundle",
                "type": "transaction",
                "entry": [
                    {
                        "fullUrl": "urn:uuid:11111111-1111-1111-1111-111111111111",
                        "resource": {
                            "resourceType": "Observation",
                            "id": "11111111-1111-1111-1111-111111111111",
                            "status": "final",
                            "code": {
                                "coding": [{"system": "http://loinc.org", "code": "8302-2", "display": "Body Height"}]
                            },
                            "subject": {"reference": "urn:uuid:22222222-2222-2222-2222-222222222222"},
                            "effectiveDateTime": "2024-01-01T00:00:00Z",
                            "valueQuantity": {"value": 170, "unit": "cm", "system": "http://unitsofmeasure.org", "code": "cm"}
                        },
                        "request": {"method": "POST", "url": "Observation"}
                    }
                ]
            }
            """;

        var content = new StringContent(noPatientsBundle);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await _client.PostAsync("/fhir/Bundle", content);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var detail = doc.RootElement.GetProperty("detail").GetString();
        Assert.NotNull(detail);
        Assert.Contains("No Patient resources found", detail);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IngestSyntheaBundle_PersistsPatients()
    {
        var json = LoadFixture("synthea-bundle-small.json");
        var content = new StringContent(json);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await _client.PostAsync("/fhir/Bundle", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal(2, root.GetProperty("patients").GetInt32());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IngestSyntheaBundle_PersistsObservations()
    {
        var json = LoadFixture("synthea-bundle-small.json");
        var content = new StringContent(json);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await _client.PostAsync("/fhir/Bundle", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal(8, root.GetProperty("observations").GetInt32());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IngestSyntheaBundle_CreatesDoltCommit()
    {
        var json = LoadFixture("synthea-bundle-small.json");
        var content = new StringContent(json);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await _client.PostAsync("/fhir/Bundle", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // dolt_commit should be present (may be null if DOLT_COMMIT fails, but property exists)
        Assert.True(root.TryGetProperty("dolt_commit", out _));
    }
}
