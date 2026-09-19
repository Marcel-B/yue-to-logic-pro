using YueToLogic.Api;

const string CorsPolicy = "ConfiguredOrigins";

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = PublishedContentRoot(),
});

builder.Services.AddYueToLogic();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Clients served from another origin (e.g. an Electron shell or a native app's web view) must be listed
// under Cors:AllowedOrigins. The bundled web frontend is served by this app itself and needs no entry.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().WithMethods("GET", "POST")));

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
