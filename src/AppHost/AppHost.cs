var builder = DistributedApplication.CreateBuilder(args);

// DoltgreSQL — version-controlled PostgreSQL-compatible database
// Every change to clinical data is tracked, diffable, and revertable
var doltgres = builder.AddContainer("doltgresql", "dolthub/doltgresql", "latest")
    .WithEndpoint(port: 5432, targetPort: 5432, name: "tcp", scheme: "tcp")
    .WithVolume("doltgres-data", "/var/lib/dolt")
    .WithLifetime(ContainerLifetime.Persistent);

var redis = builder.AddRedis("cache")
    .WithLifetime(ContainerLifetime.Persistent);

// C# services — connect to DoltgreSQL via standard Npgsql (PG wire protocol)
var fhirIngest = builder.AddProject<Projects.Acme_Stack_FhirIngest>("fhir-ingest")
    .WithReference(redis)
    .WithHttpHealthCheck("/health");

var dataApi = builder.AddProject<Projects.Acme_Stack_DataApi>("data-api")
    .WithReference(redis)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
