var builder = DistributedApplication.CreateBuilder(args);

// DoltgreSQL — version-controlled PostgreSQL-compatible database
// Every change to clinical data is tracked, diffable, and revertable
var doltgres = builder.AddContainer("doltgresql", "dolthub/doltgresql", "0.55.6")
    .WithEndpoint(port: 5432, targetPort: 5432, name: "tcp", scheme: "tcp")
    .WithEnvironment("DOLT_ROOT_PATH", "/var/lib/dolt")
    .WithVolume("doltgres-data", "/var/lib/dolt")
    .WithLifetime(ContainerLifetime.Persistent);

var redis = builder.AddRedis("cache")
    .WithLifetime(ContainerLifetime.Persistent);

// DoltgreSQL connection string — manual because AddContainer doesn't implement
// IResourceWithConnectionString (unlike AddPostgres). See design.md Gotchas.
// ReferenceExpression defers endpoint resolution to runtime (container start).
var doltgresEndpoint = doltgres.GetEndpoint("tcp");
var doltgresConnStr = ReferenceExpression.Create(
    $"Host={doltgresEndpoint.Property(EndpointProperty.Host)};Port={doltgresEndpoint.Property(EndpointProperty.Port)};Username=root;Database=acme_health");

// C# services — connect to DoltgreSQL via standard Npgsql (PG wire protocol)
// WaitFor gates service startup on container "started" state, not TCP readiness.
// DoltgreSQL needs ~2-3s after start to accept PG wire connections.
// Services handle this via try/catch on startup schema creation.
var fhirIngest = builder.AddProject<Projects.Acme_Stack_FhirIngest>("fhir-ingest")
    .WithReference(redis)
    .WithEnvironment("ConnectionStrings__doltgresql", doltgresConnStr)
    .WaitFor(doltgres)
    .WithHttpHealthCheck("/health");

var dataApi = builder.AddProject<Projects.Acme_Stack_DataApi>("data-api")
    .WithReference(redis)
    .WithEnvironment("ConnectionStrings__doltgresql", doltgresConnStr)
    .WaitFor(doltgres)
    .WithHttpHealthCheck("/health");

// Python services — FastAPI apps managed by uv, connecting via psycopg (PG wire)
var wearableNormalizer = builder.AddUvicornApp(
        "wearable-normalizer",
        "../services/wearable-normalizer",
        "main:app")
    .WithUv()
    .WithHttpEndpoint(port: 8001, name: "http")
    .WithEnvironment("ConnectionStrings__doltgresql", doltgresConnStr)
    .WaitFor(doltgres)
    .WithHttpHealthCheck("/health");

var clinicalExtractor = builder.AddUvicornApp(
        "clinical-extractor",
        "../services/clinical-extractor",
        "main:app")
    .WithUv()
    .WithHttpEndpoint(port: 8002, name: "http")
    .WithEnvironment("ConnectionStrings__doltgresql", doltgresConnStr)
    .WaitFor(doltgres)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
