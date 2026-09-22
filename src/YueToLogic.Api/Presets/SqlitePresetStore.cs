using System.Text.Json;
using Microsoft.Data.Sqlite;
using YueToLogic.Api.Data;

namespace YueToLogic.Api.Presets;

/// <summary>
/// The presets in the application's SQLite file, one row per user and name. Names are matched through
/// <c>name_key</c>, the name in invariant upper case, because SQLite itself folds only ASCII letters.
/// </summary>
public sealed class SqlitePresetStore(SqliteDatabase database) : IPresetStore
{
    private const string Columns = "id, name, form, updated_at";

    public IReadOnlyList<Preset> List(string userId)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM presets WHERE user_id = $user ORDER BY name_key, id";
        command.Parameters.AddWithValue("$user", userId);
        using var reader = command.ExecuteReader();
        var presets = new List<Preset>();
        while (reader.Read())
        {
            presets.Add(Read(reader));
        }

        return presets;
    }

    public (Preset Preset, bool Created) Save(string userId, string name, JsonElement form)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        // The name's spelling follows the latest save, its identity does not: "live" replaces "Live".
        var key = Key(name);
        var existed = SqliteDatabase.Execute(
            connection,
            """
            UPDATE presets SET name = $name, form = $form, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            WHERE user_id = $user AND name_key = $key
            """,
            ("$user", userId),
            ("$key", key),
            ("$name", name),
            ("$form", form.GetRawText())) > 0;
        if (!existed)
        {
            SqliteDatabase.Execute(
                connection,
                "INSERT INTO presets (user_id, name, name_key, form) VALUES ($user, $name, $key, $form)",
                ("$user", userId),
                ("$name", name),
                ("$key", key),
                ("$form", form.GetRawText()));
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM presets WHERE user_id = $user AND name_key = $key";
        command.Parameters.AddWithValue("$user", userId);
        command.Parameters.AddWithValue("$key", key);
        using var reader = command.ExecuteReader();
        reader.Read();
        var preset = Read(reader);
        reader.Close();
        transaction.Commit();
        return (preset, !existed);
    }

    public bool Delete(string userId, string name)
    {
        using var connection = database.Open();
        return SqliteDatabase.Execute(connection, "DELETE FROM presets WHERE user_id = $user AND name_key = $key", ("$user", userId), ("$key", Key(name))) > 0;
    }

    /// <summary>Upper case is what .NET recommends for comparing without regard to case; it folds "ä" to "Ä", which SQLite would not.</summary>
    private static string Key(string name) => name.ToUpperInvariant();

    /// <summary>A row in the column order of <see cref="Columns"/>; the form is parsed so that it goes out as JSON, not as a string of JSON.</summary>
    private static Preset Read(SqliteDataReader reader)
    {
        using var form = JsonDocument.Parse(reader.GetString(2));
        return new Preset(reader.GetInt64(0), reader.GetString(1), form.RootElement.Clone(), reader.GetString(3));
    }
}
