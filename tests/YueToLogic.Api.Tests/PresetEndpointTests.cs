using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using YueToLogic.Api.Instruments;
using YueToLogic.Api.Presets;

namespace YueToLogic.Api.Tests;

public class PresetEndpointTests : IDisposable
{
    private readonly PresetApp _app = new();
    private readonly HttpClient _client;

    public PresetEndpointTests()
    {
        _client = _app.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _app.Dispose();
    }

    [Fact]
    public async Task A_preset_is_saved_listed_replaced_and_deleted()
    {
        var created = await _client.PutAsJsonAsync("/api/presets/Live%20Set", new { form = new { bass = "Walking", swing = 30 } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("/api/presets/Live%20Set", created.Headers.Location!.ToString());
        var preset = (await created.Content.ReadFromJsonAsync<Preset>())!;
        Assert.Equal("Live Set", preset.Name);
        Assert.Equal("Walking", preset.Form.GetProperty("bass").GetString());

        // The list hands the form back as JSON, not as a string holding JSON.
        var listed = await _client.GetStringAsync("/api/presets");
        Assert.Contains("\"form\":{\"bass\":\"Walking\",\"swing\":30}", listed, StringComparison.Ordinal);

        // Saving under the same name, however spelled, replaces it and takes the new spelling.
        var replaced = await _client.PutAsJsonAsync("/api/presets/live%20set", new { form = new { bass = "off" } });
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        var presets = (await _client.GetFromJsonAsync<Preset[]>("/api/presets"))!;
        var only = Assert.Single(presets);
        Assert.Equal((preset.Id, "live set", "off"), (only.Id, only.Name, only.Form.GetProperty("bass").GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync("/api/presets/LIVE%20SET")).StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<Preset[]>("/api/presets"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/api/presets/Live%20Set")).StatusCode);
    }

    [Fact]
    public async Task Names_are_matched_without_regard_to_case_beyond_ascii_too()
    {
        // SQLite's own NOCASE folds ASCII only; "Ä" and "ä" must still be the one preset.
        await _client.PutAsJsonAsync("/api/presets/Gro%C3%9Fe%20B%C3%BChne", new { form = new { swing = 1 } });
        var replaced = await _client.PutAsJsonAsync("/api/presets/GRO%C3%9FE%20B%C3%9CHNE", new { form = new { swing = 2 } });

        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        var only = Assert.Single((await _client.GetFromJsonAsync<Preset[]>("/api/presets"))!);
        Assert.Equal(("GROßE BÜHNE", 2), (only.Name, only.Form.GetProperty("swing").GetInt32()));
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync("/api/presets/gro%C3%9Fe%20b%C3%BChne")).StatusCode);
    }

    [Fact]
    public async Task Presets_are_listed_by_name_and_survive_a_restart()
    {
        await _client.PutAsJsonAsync("/api/presets/Zweite", new { form = new { swing = 2 } });
        await _client.PutAsJsonAsync("/api/presets/erste", new { form = new { swing = 1 } });

        using var restarted = new PresetApp(_app.DataPath);
        using var client = restarted.CreateClient();
        var names = (await client.GetFromJsonAsync<Preset[]>("/api/presets"))!.Select(preset => preset.Name);

        Assert.Equal(["erste", "Zweite"], names);
    }

    [Theory]
    [InlineData("/api/presets/%20%20", "{\"form\":{}}", "name must not be empty")]
    [InlineData("/api/presets/X", "{\"form\":\"not an object\"}", "form must be a JSON object")]
    [InlineData("/api/presets/X", "{}", "form must be a JSON object")]
    public async Task An_invalid_preset_is_refused_and_nothing_is_stored(string url, string body, string problem)
    {
        var response = await _client.PutAsync(url, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(problem, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty((await _client.GetFromJsonAsync<Preset[]>("/api/presets"))!);
    }

    [Fact]
    public async Task A_name_longer_than_allowed_is_refused()
    {
        var response = await _client.PutAsJsonAsync($"/api/presets/{new string('x', PresetInput.MaxNameLength + 1)}", new { form = new { } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_file_from_before_presets_existed_is_upgraded_and_keeps_its_instruments()
    {
        // The schema of version 2, as the instrument store wrote it before there were users and presets.
        var path = Path.Combine(Path.GetTempPath(), $"yue-to-logic-tests-{Guid.NewGuid():N}", "yue-to-logic.db");
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
                    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                    kind TEXT NOT NULL DEFAULT 'Synth',
                    drum_kick INTEGER, drum_snare INTEGER, drum_closed_hihat INTEGER, drum_open_hihat INTEGER, drum_crash INTEGER, drum_clap INTEGER
                );
                CREATE TABLE track_assignments (
                    track TEXT NOT NULL COLLATE NOCASE PRIMARY KEY,
                    instrument_id INTEGER NOT NULL REFERENCES instruments (id) ON DELETE CASCADE
                );
                INSERT INTO instruments (name, port, channel) VALUES ('Mother32', 'MIDI4x4 Midi Out 1', 12);
                PRAGMA user_version = 2;
                """;
            await command.ExecuteNonQueryAsync();
        }

        using var app = new PresetApp(path);
        using var client = app.CreateClient();
        var saved = await client.PutAsJsonAsync("/api/presets/Nach%20dem%20Upgrade", new { form = new { drums = "Backbeat" } });
        var instruments = (await client.GetFromJsonAsync<Instrument[]>("/api/instruments"))!;

        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        Assert.Equal("Mother32", Assert.Single(instruments).Name);
        Assert.Equal(JsonValueKind.Object, (await client.GetFromJsonAsync<Preset[]>("/api/presets"))!.Single().Form.ValueKind);

        SqliteConnection.ClearAllPools();
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    private sealed class PresetApp : WebApplicationFactory<Program>
    {
        private readonly bool _owner;

        public PresetApp()
            : this(Path.Combine(Path.GetTempPath(), $"yue-to-logic-tests-{Guid.NewGuid():N}", "presets.db"))
        {
            _owner = true;
        }

        public PresetApp(string dataPath)
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
                SqliteConnection.ClearAllPools();
                if (Path.GetDirectoryName(DataPath) is { } directory && Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }
}
