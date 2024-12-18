using System.Text.Json;
using System.Text.Json.Serialization;
using JasperFx.Core;
using Marten;
using Marten.Events;
using Marten.Services;
using MartenIdDemo.Events;
using Weasel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMarten(
    martenOptions =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Please provide a connection string to Postgres via ConnectionStrings:DefaultConnection");

        martenOptions.Connection(connectionString);
        martenOptions.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;

        martenOptions.Events.EnableUniqueIndexOnEventId = true;
        martenOptions.Events.StreamIdentity = StreamIdentity.AsString;

        martenOptions.OpenTelemetry.TrackEventCounters();
        martenOptions.OpenTelemetry.TrackConnections = TrackLevel.Normal;

        martenOptions.UseSystemTextJsonForSerialization(
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });
    });

var app = builder.Build();

var docStore = app.Services.GetRequiredService<IDocumentStore>();
await docStore.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

app.MapGet("/", async (IDocumentStore documentStore) =>
{
    var id = Guid.NewGuid();

    var evt = new UserEvents.SignedUp(id, Guid.NewGuid().ToString());

    await using (var session = documentStore.LightweightSession())
    {
        session.Events.Append(evt.UserId, evt);
        await session.SaveChangesAsync();
    }
    
    await using (var session = documentStore.LightweightSession())
    {
        var returnedEvt = await session
            .Events
            .QueryAllRawEvents()
            .Where(x => x.Id == id)
            .ToListAsync();

        return Results.Ok(returnedEvt);

    }
});

app.Run();