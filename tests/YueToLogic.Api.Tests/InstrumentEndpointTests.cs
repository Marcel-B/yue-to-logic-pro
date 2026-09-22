using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using YueToLogic.Api.Instruments;
using YueToLogic.Core.Arrangement;

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
    public async Task A_drum_machine_keeps_the_notes_of_its_drums()
    {
        var created = await _client.PostAsJsonAsync("/api/instruments", new
        {
            name = "DrumBrute Impact",
            port = "MIDI4x4 Midi Out 2",
            channel = 8,
            kind = "DrumMachine",
            drums = new { kick = 36, snare = 37, closedHiHat = 44, openHiHat = 45, crash = 51, clap = 39 },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var expected = new Instrument(0, "DrumBrute Impact", "MIDI4x4 Midi Out 2", 8, InstrumentKind.DrumMachine,
            new DrumNotes { Kick = 36, Snare = 37, ClosedHiHat = 44, OpenHiHat = 45, Crash = 51, Clap = 39 });
        var instrument = (await created.Content.ReadFromJsonAsync<Instrument>())!;
        Assert.Equal(expected with { Id = instrument.Id }, instrument);
        Assert.Contains("\"kind\":\"DrumMachine\"", await created.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // The list and a restart give the same back.
        Assert.Equal([instrument], (await _client.GetFromJsonAsync<Instrument[]>("/api/instruments"))!);
        using var restarted = new InstrumentApp(_app.DataPath);
        using var client = restarted.CreateClient();
        Assert.Equal([instrument], (await client.GetFromJsonAsync<Instrument[]>("/api/instruments"))!);
    }

    [Fact]
    public async Task A_drum_machine_without_notes_gets_general_midi_and_a_synthesizer_has_none()
    {
        var machine = await _client.PostAsJsonAsync("/api/instruments", new { name = "TR-8", port = "MIDI4x4 Midi Out 3", channel = 10, kind = "DrumMachine" });
        var synth = await _client.PostAsJsonAsync("/api/instruments", new { name = "Mother32", port = "MIDI4x4 Midi Out 1", channel = 12, drums = new { kick = 50 } });

        var tr8 = (await machine.Content.ReadFromJsonAsync<Instrument>())!;
        var mother = (await synth.Content.ReadFromJsonAsync<Instrument>())!;
        Assert.Equal(new DrumNotes(), tr8.Drums);
        Assert.Equal(InstrumentKind.Synth, mother.Kind);
        Assert.Null(mother.Drums);

        // Turning the synthesizer into a drum machine and back drops the notes again.
        var changed = await _client.PutAsJsonAsync($"/api/instruments/{mother.Id}", new { name = "Mother32", port = "MIDI4x4 Midi Out 1", channel = 12, kind = "DrumMachine", drums = new { kick = 50 } });
        Assert.Equal(50, (await changed.Content.ReadFromJsonAsync<Instrument>())!.Drums!.Kick);
        var back = await _client.PutAsJsonAsync($"/api/instruments/{mother.Id}", new { name = "Mother32", port = "MIDI4x4 Midi Out 1", channel = 12, kind = "Synth" });
        Assert.Null((await back.Content.ReadFromJsonAsync<Instrument>())!.Drums);
    }

    [Theory]
    [InlineData("DrumMachine", 128, "drums.kick")]
    [InlineData("DrumMachine", -1, "drums.kick")]
    [InlineData("Piano", 36, "kind")]
    public async Task A_drum_note_off_the_keyboard_or_an_unknown_kind_is_refused(string kind, int kick, string field)
    {
        var response = await _client.PostAsJsonAsync("/api/instruments", new { name = "X", port = "MIDI4x4 Midi Out 2", channel = 8, kind, drums = new { kick } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty((await _client.GetFromJsonAsync<Instrument[]>("/api/instruments"))!);
    }

    [Fact]
    public async Task A_library_from_the_first_release_is_upgraded_in_place()
    {
        // The schema of version 1, as SqliteInstrumentStore created it before instruments had a kind.
        var path = Path.Combine(Path.GetTempPath(), $"yue-to-logic-tests-{Guid.NewGuid():N}", "instruments.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
        {
            connection.Open();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE instruments (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    port TEXT NOT NULL,
                    channel INTEGER NOT NULL CHECK (channel BETWEEN 1 AND 16),
                    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                );
                CREATE TABLE track_assignments (
                    track TEXT NOT NULL COLLATE NOCASE PRIMARY KEY,
                    instrument_id INTEGER NOT NULL REFERENCES instruments (id) ON DELETE CASCADE
                );
                INSERT INTO instruments (name, port, channel) VALUES ('Mother32', 'MIDI4x4 Midi Out 1', 12);
                INSERT INTO track_assignments (track, instrument_id) VALUES ('Bass', 1);
                PRAGMA user_version = 1;
                """;
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            using var app = new InstrumentApp(path);
            using var client = app.CreateClient();

            Assert.Equal([new Instrument(1, "Mother32", "MIDI4x4 Midi Out 1", 12)], (await client.GetFromJsonAsync<Instrument[]>("/api/instruments"))!);
            Assert.Equal(new Dictionary<string, long> { ["Bass"] = 1 }, await client.GetFromJsonAsync<Dictionary<string, long>>("/api/instruments/assignments"));
            var machine = await client.PostAsJsonAsync("/api/instruments", new { name = "DrumBrute", port = "MIDI4x4 Midi Out 2", channel = 8, kind = "DrumMachine", drums = new { snare = 37 } });
            Assert.Equal(HttpStatusCode.Created, machine.StatusCode);
            Assert.Equal(37, (await machine.Content.ReadFromJsonAsync<Instrument>())!.Drums!.Snare);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
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
