var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin();

var db = postgres.AddDatabase("familycalendardb");

var api = builder.AddProject<Projects.FamilyCalendar_Api>("familycalendar-api")
    .WithReference(db)
    .WaitFor(db);

builder.AddNpmApp("familycalendar-web", "../../src/FamilyCalendar.Web")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
