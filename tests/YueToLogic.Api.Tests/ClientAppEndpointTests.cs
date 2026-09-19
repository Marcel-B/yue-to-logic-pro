using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace YueToLogic.Api.Tests;

public sealed class ClientAppEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string IndexHtml = "<!doctype html><div id=\"app\"></div>";

    private readonly string _webRoot = Directory.CreateTempSubdirectory("yue-webroot-").FullName;

    [Fact]
    public async Task Root_redirects_to_the_web_interface()
    {
        var client = CreateClient(withBuild: true, allowRedirects: false);

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/ui/", response.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData("/ui")]
    [InlineData("/ui/")]
    [InlineData("/ui/some/client/route")]
    public async Task Paths_below_ui_serve_the_app_shell(string path)
    {
        var response = await CreateClient(withBuild: true).GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(IndexHtml, await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl!.NoCache);
    }

    [Theory]
    [InlineData("/", HttpStatusCode.Redirect)]
    [InlineData("/ui/", HttpStatusCode.OK)]
    [InlineData("/api/health", HttpStatusCode.OK)]
    // Only the status is checked: TestServer, unlike Kestrel, does not strip the body of HEAD responses.
    public async Task Head_requests_are_answered_for_uptime_monitors(string path, HttpStatusCode expected)
    {
        var client = CreateClient(withBuild: true, allowRedirects: false);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, path));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Built_assets_are_served_as_static_files()
    {
        var response = await CreateClient(withBuild: true).GetAsync("/ui/assets/index-abc123.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("console.log('ui')", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_asset_is_not_answered_with_the_app_shell()
    {
        var response = await CreateClient(withBuild: true).GetAsync("/ui/assets/missing.js");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unbuilt_web_interface_explains_how_to_build_it()
    {
        var response = await CreateClient(withBuild: false).GetAsync("/ui/");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("dotnet publish", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    private HttpClient CreateClient(bool withBuild, bool allowRedirects = true)
    {
        if (withBuild)
        {
            Directory.CreateDirectory(Path.Combine(_webRoot, "ui", "assets"));
            File.WriteAllText(Path.Combine(_webRoot, "ui", "index.html"), IndexHtml);
            File.WriteAllText(Path.Combine(_webRoot, "ui", "assets", "index-abc123.js"), "console.log('ui')");
        }

        return factory
            .WithWebHostBuilder(builder => builder.UseSetting(WebHostDefaults.WebRootKey, _webRoot))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowRedirects });
    }
}
