var builder = DistributedApplication.CreateBuilder(args);

// Dolt MySQL — version-controlled MySQL-compatible database
// Every change to clinical data is tracked, diffable, and revertable
var dbPassword = builder.AddParameter("dolt-password", secret: true);
var dolt = builder.AddMySql("dolt", password: dbPassword)
    .WithImage("dolthub/dolt-sql-server", "v1.50.2")
    .WithImageRegistry("docker.io")
    .WithEnvironment("DOLT_ROOT_HOST", "%")
    .WithDataVolume("dolt-data")
    .WithLifetime(ContainerLifetime.Persistent);
var db = dolt.AddDatabase("acme-health", databaseName: "acme_health");

var redis = builder.AddRedis("cache")
    .WithLifetime(ContainerLifetime.Persistent);

// C# services — connect to Dolt via MySqlConnector (MySQL wire protocol)
// WaitFor gates service startup on MySQL TCP health check (native with AddMySql).
var fhirIngest = builder.AddProject<Projects.Acme_Stack_FhirIngest>("fhir-ingest")
    .WithReference(redis)
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health");

var dataApi = builder.AddProject<Projects.Acme_Stack_DataApi>("data-api")
    .WithReference(redis)
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health");

// Python services — FastAPI apps managed by uv, connecting via aiomysql (MySQL wire)
var wearableNormalizer = builder.AddUvicornApp(
        "wearable-normalizer",
        "../services/wearable-normalizer",
        "main:app")
    .WithUv()
    .WithEndpoint("http", e => e.Port = 8001)
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health");

var clinicalExtractor = builder.AddUvicornApp(
        "clinical-extractor",
        "../services/clinical-extractor",
        "main:app")
    .WithUv()
    .WithEndpoint("http", e => e.Port = 8002)
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
