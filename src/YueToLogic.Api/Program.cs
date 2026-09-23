using YueToLogic.Api;
using YueToLogic.Api.Data;
using YueToLogic.Api.Instruments;
using YueToLogic.Api.Presets;
using YueToLogic.Core.Stems;
using YueToLogic.Core.Voices;

const string CorsPolicy = "ConfiguredOrigins";

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = PublishedContentRoot(),
});

builder.Services.AddYueToLogic();

// The instruments (a name for a MIDI port and channel), their tracks and the presets of the web form are the
// only state the app keeps: one SQLite file, created when it is first needed. Data:Path (Data__Path in a
// container) says where; the default suits development, the container image points it at its /data volume.
var dataPath = builder.Configuration["Data:Path"];
if (string.IsNullOrWhiteSpace(dataPath))
{
    dataPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "yue-to-logic.db");
}
var database = new SqliteDatabase(dataPath);
builder.Services.AddSingleton<IInstrumentStore>(new SqliteInstrumentStore(database));
builder.Services.AddSingleton<IPresetStore>(new SqlitePresetStore(database));
// No login yet: everything that has an owner belongs to the one local user. See ICurrentUser.
builder.Services.AddSingleton<ICurrentUser, LocalUser>();

// Stem separation is optional: without an address and a key the endpoints answer that this server has none.
var stems = builder.Configuration.GetSection("Stems");
if (Uri.TryCreate(stems["BaseUrl"], UriKind.Absolute, out var stemService) && !string.IsNullOrWhiteSpace(stems["ApiKey"]))
{
    builder.Services.AddHttpClient<IStemSeparationService, StemSeparationService>(client =>
    {
        client.BaseAddress = stemService;
        client.DefaultRequestHeaders.Add("X-Api-Key", stems["ApiKey"]);
        // A separation runs for minutes, but every call here only starts, asks or fetches.
        client.Timeout = TimeSpan.FromMinutes(10);
    });
}
// Changing a voice is optional in the same way: without an address and a key the endpoints answer that this
// server has none, and the interface leaves the whole section out.
var voice = builder.Configuration.GetSection("Voice");
if (Uri.TryCreate(voice["BaseUrl"], UriKind.Absolute, out var voiceService) && !string.IsNullOrWhiteSpace(voice["ApiKey"]))
{
    builder.Services.AddHttpClient<IVoiceConversionService, VoiceConversionService>(client =>
    {
        client.BaseAddress = voiceService;
        client.DefaultRequestHeaders.Add("X-Api-Key", voice["ApiKey"]);
        // A conversion runs for minutes, but every call here only starts, asks or fetches.
        client.Timeout = TimeSpan.FromMinutes(10);
    });
}
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Clients served from another origin (e.g. an Electron shell or a native app's web view) must be listed
// under Cors:AllowedOrigins. The bundled web frontend is served by this app itself and needs no entry.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().WithMethods("GET", "POST", "PUT", "DELETE")
        .WithExposedHeaders(ConvertEndpoints.DiagnosticsHeader, "Content-Disposition")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseCors(CorsPolicy);
app.UseStaticFiles();

app.MapOpenApi("/api/openapi");

var api = app.MapGroup("/api");
// HEAD as well, since uptime monitors often probe with it.
api.MapMethods("/health", ClientAppEndpoints.GetAndHead, () => Results.Text("ok"));
api.MapConvertEndpoints();
api.MapStemEndpoints();
api.MapVoiceEndpoints();
api.MapInstrumentEndpoints();
api.MapPresetEndpoints();

app.MapClientApp();

app.Run();

// ASP.NET Core looks for appsettings.json and wwwroot in the working directory. A published app started from
// elsewhere (e.g. by a desktop shell) would then miss its web frontend, so use the app's own directory instead.
static string? PublishedContentRoot()
{
    const string settings = "appsettings.json";
    var appDirectory = AppContext.BaseDirectory;
    return !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), settings)) && File.Exists(Path.Combine(appDirectory, settings))
        ? appDirectory
        : null;
}

/// <summary>Entry point; public so that integration tests can start the app with WebApplicationFactory.</summary>
public partial class Program;
