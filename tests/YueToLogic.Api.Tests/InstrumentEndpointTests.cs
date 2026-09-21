using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using YueToLogic.Api.Instruments;

namespace YueToLogic.Api.Tests;

public class InstrumentEndpointTests : IDisposable
{
    private readonly InstrumentApp _app = new();
    private readonly HttpClient _client;

    public InstrumentEndpointTests()
    {
        _client = _app.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _app.Dispose();
    }

    [Fact]
    public async Task An_instrument_is_created_listed_changed_and_deleted()
    {
        var created = await _client.PostAsJsonAsync("/api/instruments", new { name = " Mother32 ", port = "MIDI4x4 Midi Out 1", channel = 12 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var instrument = (await created.Content.ReadFromJsonAsync<Instrument>())!;
        Assert.Equal("Mother32", instrument.Name);
        Assert.Equal($"/api/instruments/{instrument.Id}", created.Headers.Location!.ToString());

        var listed = await _client.GetFromJsonAsync<Instrument[]>("/api/instruments");
        Assert.Equal([instrument], listed!);

        var changed = await _client.PutAsJsonAsync($"/api/instruments/{instrument.Id}", new { name = "Mother-32", port = "Scarlett 8i6 USB", channel = 3 });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(new Instrument(instrument.Id, "Mother-32", "Scarlett 8i6 USB", 3), await changed.Content.ReadFromJsonAsync<Instrument>());

        var deleted = await _client.DeleteAsync($"/api/instruments/{instrument.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<Instrument[]>("/api/instruments"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/instruments/{instrument.Id}")).StatusCode);
    }

    [Fact]
    public async Task Instruments_are_listed_by_name()
    {
        await _client.PostAsJsonAsync("/api/instruments", new { name = "WASP Deluxe", port = "Scarlett 8i6 USB", channel = 1 });
        await _client.PostAsJsonAsync("/api/instruments", new { name = "mother32", port = "MIDI4x4 Midi Out 1", channel = 12 });

        var names = (await _client.GetFromJsonAsync<Instrument[]>("/api/instruments"))!.Select(i => i.Name);

        Assert.Equal(["mother32", "WASP Deluxe"], names);
    }

    [Fact]
    public async Task A_second_instrument_with_the_same_name_is_refused()
    {
        await _client.PostAsJsonAsync("/api/instruments", new { name = "Mother32", port = "MIDI4x4 Midi Out 1", channel = 12 });
        var other = await _client.PostAsJsonAsync("/api/instruments", new { name = "WASP", port = "Scarlett 8i6 USB", channel = 1 });
        var otherId = (await other.Content.ReadFromJsonAsync<Instrument>())!.Id;

        var again = await _client.PostAsJsonAsync("/api/instruments", new { name = "mother32", port = "Scarlett 8i6 USB", channel = 2 });
        var renamed = await _client.PutAsJsonAsync($"/api/instruments/{otherId}", new { name = "MOTHER32", port = "Scarlett 8i6 USB", channel = 1 });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, renamed.StatusCode);
        Assert.Equal(2, (await _client.GetFromJsonAsync<Instrument[]>("/api/instruments"))!.Length);
    }

    [Theory]
    [InlineData("", "MIDI4x4 Midi Out 1", 12, "name")]
    [InlineData("Mother32", "  ", 12, "port")]
    [InlineData("Mother32", "MIDI4x4 Midi Out 1", 0, "channel")]
    [InlineData("Mother32", "MIDI4x4 Midi Out 1", 17, "channel")]
    public async Task Invalid_input_is_refused_and_the_field_is_named(string name, string port, int channel, string field)
    {
        var response = await _client.PostAsJsonAsync("/api/instruments", new { name, port, channel });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty((await _client.GetFromJsonAsync<Instrument[]>("/api/instruments"))!);
    }

    [Fact]
    public async Task A_missing_channel_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/instruments", new { name = "Mother32", port = "MIDI4x4 Midi Out 1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Changing_an_unknown_instrument_is_a_not_found()
    {
        var response = await _client.PutAsJsonAsync("/api/instruments/42", new { name = "Mother32", port = "MIDI4x4 Midi Out 1", channel = 12 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_track_is_assigned_and_the_assignment_is_listed()
    {
        var mother = await CreateAsync("Mother32", "MIDI4x4 Midi Out 1", 12);
        var wasp = await CreateAsync("WASP Deluxe", "Scarlett 8i6 USB", 1);

        Assert.Equal(HttpStatusCode.NoContent, (await AssignAsync("Bass", mother)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await AssignAsync("Vocal 8vb", wasp)).StatusCode);
        // Assigning again replaces the earlier choice.
        Assert.Equal(HttpStatusCode.NoContent, (await AssignAsync("Bass", wasp)).StatusCode);

        var assignments = await _client.GetFromJsonAsync<Dictionary<string, long>>("/api/instruments/assignments");
        Assert.Equal(new Dictionary<string, long> { ["Bass"] = wasp, ["Vocal 8vb"] = wasp }, assignments);
    }

    [Fact]
    public async Task Assigning_an_unknown_instrument_is_a_not_found()
    {
        var response = await AssignAsync("Bass", 42);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<Dictionary<string, long>>("/api/instruments/assignments"))!);
    }

    [Fact]
    public async Task Assigning_null_takes_the_assignment_away()
    {
        var mother = await CreateAsync("Mother32", "MIDI4x4 Midi Out 1", 12);
        await AssignAsync("Bass", mother);

        var response = await _client.PutAsJsonAsync("/api/instruments/assignments/Bass", new { instrumentId = (long?)null });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<Dictionary<string, long>>("/api/instruments/assignments"))!);
    }

    [Fact]
    public async Task Deleting_an_instrument_drops_its_assignments()
    {
        var mother = await CreateAsync("Mother32", "MIDI4x4 Midi Out 1", 12);
        var wasp = await CreateAsync("WASP Deluxe", "Scarlett 8i6 USB", 1);
        await AssignAsync("Bass", mother);
        await AssignAsync("Vocal", wasp);

        await _client.DeleteAsync($"/api/instruments/{mother}");

        var assignments = await _client.GetFromJsonAsync<Dictionary<string, long>>("/api/instruments/assignments");
        Assert.Equal(new Dictionary<string, long> { ["Vocal"] = wasp }, assignments);
    }

    [Fact]
    public async Task A_blank_track_name_is_refused()
    {
        var mother = await CreateAsync("Mother32", "MIDI4x4 Midi Out 1", 12);

        var response = await AssignAsync("%20", mother);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Instruments_survive_a_restart()
    {
        var mother = await CreateAsync("Mother32", "MIDI4x4 Midi Out 1", 12);
        await AssignAsync("Bass", mother);

        using var restarted = new InstrumentApp(_app.DataPath);
        using var client = restarted.CreateClient();

        Assert.Equal([new Instrument(mother, "Mother32", "MIDI4x4 Midi Out 1", 12)], (await client.GetFromJsonAsync<Instrument[]>("/api/instruments"))!);
        Assert.Equal(new Dictionary<string, long> { ["Bass"] = mother }, await client.GetFromJsonAsync<Dictionary<string, long>>("/api/instruments/assignments"));
    }

    private async Task<long> CreateAsync(string name, string port, int channel)
    {
        var response = await _client.PostAsJsonAsync("/api/instruments", new { name, port, channel });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Instrument>())!.Id;
    }

    private Task<HttpResponseMessage> AssignAsync(string track, long instrumentId) =>
        _client.PutAsJsonAsync($"/api/instruments/assignments/{track}", new { instrumentId });

    /// <summary>The app with its instrument library in a temporary file of its own, removed with the test.</summary>
    private sealed class InstrumentApp : WebApplicationFactory<Program>
    {
        private readonly bool _owner;

        public InstrumentApp()
            : this(Path.Combine(Path.GetTempPath(), $"yue-to-logic-tests-{Guid.NewGuid():N}", "instruments.db"))
        {
            _owner = true;
        }

        public InstrumentApp(string dataPath)
        {
            DataPath = dataPath;
        }

        public string DataPath { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseSetting("Data:Path", DataPath);

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && _owner)
            {
                // Pooled connections keep the file open, and on Windows a directory with an open file cannot go.
                SqliteConnection.ClearAllPools();
                if (Path.GetDirectoryName(DataPath) is { } directory && Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }
}
