using Microsoft.Data.Sqlite;

namespace YueToLogic.Api.Data;

/// <summary>
/// The one SQLite file the application keeps its state in: instruments, track assignments and presets. The
/// file and its tables are created on first use, so a server whose data directory is not writable still
/// starts and converts; only the endpoints that need the file fail then.
/// </summary>
/// <remarks>
/// Every call opens its own connection; Microsoft.Data.Sqlite pools them, and SQLite's file lock serializes
/// the writers. The schema carries a version in <c>PRAGMA user_version</c> so that later changes can be
/// applied in order to a file an earlier release wrote: version 2 added the instrument's kind and, for a
/// drum machine, one column per drum with the note it plays on - a synthesizer leaves them NULL. Version 3
/// added users and presets: every preset belongs to a user, so that a login can be put in front of the
/// application later without touching the data; until then everything belongs to the one user
/// <see cref="Users.Local"/>, which this version also creates. Instruments are not per user yet - a studio
/// has one set of hardware - but a <c>user_id</c> column could give them an owner the same way.
/// </remarks>
public sealed class SqliteDatabase
{
    private const int SchemaVersion = 3;

    private readonly string _connectionString;
    private readonly Lock _gate = new();
    private bool _ready;

    public SqliteDatabase(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = Path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
    }

    /// <summary>Where the file is, for the log and for tests.</summary>
    public string Path { get; }

    /// <summary>An open connection with foreign keys enforced, on a file whose schema is current.</summary>
    public SqliteConnection Open()
    {
        EnsureCreated();
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        Execute(connection, "PRAGMA foreign_keys = ON");
        return connection;
    }

    public static int Execute(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command.ExecuteNonQuery();
    }

    /// <summary>Creates the directory, the file and the tables the first time the database is used.</summary>
    private void EnsureCreated()
    {
        if (_ready)
        {
            return;
        }

        lock (_gate)
        {
            if (_ready)
            {
                return;
            }

            if (System.IO.Path.GetDirectoryName(Path) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using (var version = connection.CreateCommand())
            {
                version.CommandText = "PRAGMA user_version";
                var current = (long)version.ExecuteScalar()!;
                if (current < 1)
                {
                    Execute(
                        connection,
                        """
                        CREATE TABLE IF NOT EXISTS instruments (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            name TEXT NOT NULL COLLATE NOCASE UNIQUE,
                            port TEXT NOT NULL,
                            channel INTEGER NOT NULL CHECK (channel BETWEEN 1 AND 16),
                            created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                            updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                        );
                        CREATE TABLE IF NOT EXISTS track_assignments (
                            track TEXT NOT NULL COLLATE NOCASE PRIMARY KEY,
                            instrument_id INTEGER NOT NULL REFERENCES instruments (id) ON DELETE CASCADE
                        );
                        """);
                }

                if (current < 2)
                {
                    // Version 2: the instrument's kind, and the drum notes of a drum machine. A file from version 1
                    // holds synthesizers only, which the column default says.
                    Execute(
                        connection,
                        """
                        ALTER TABLE instruments ADD COLUMN kind TEXT NOT NULL DEFAULT 'Synth';
                        ALTER TABLE instruments ADD COLUMN drum_kick INTEGER;
                        ALTER TABLE instruments ADD COLUMN drum_snare INTEGER;
                        ALTER TABLE instruments ADD COLUMN drum_closed_hihat INTEGER;
                        ALTER TABLE instruments ADD COLUMN drum_open_hihat INTEGER;
                        ALTER TABLE instruments ADD COLUMN drum_crash INTEGER;
                        ALTER TABLE instruments ADD COLUMN drum_clap INTEGER;
                        """);
                }

                if (current < 3)
                {
                    // Version 3: users, and presets that belong to one. The preset's form is kept as the JSON the
                    // web form holds - the server never reads it, so a new option needs no schema change - and a
                    // name is unique per user, not per file, so two users can both have a "Live" preset. The
                    // uniqueness is on name_key, the name folded by .NET: SQLite's NOCASE knows ASCII only, so
                    // "Ä" and "ä" would otherwise be two presets.
                    Execute(
                        connection,
                        $"""
                        CREATE TABLE IF NOT EXISTS users (
                            id TEXT NOT NULL PRIMARY KEY,
                            name TEXT NOT NULL,
                            created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                        );
                        INSERT OR IGNORE INTO users (id, name) VALUES ('{Users.Local}', '{Users.LocalName}');
                        CREATE TABLE IF NOT EXISTS presets (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            user_id TEXT NOT NULL REFERENCES users (id) ON DELETE CASCADE,
                            name TEXT NOT NULL,
                            name_key TEXT NOT NULL,
                            form TEXT NOT NULL,
                            created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                            updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                            UNIQUE (user_id, name_key)
                        );
                        """);
                }

                // Later versions add their steps here, each guarded by `current < n`, before the version is set.
                Execute(connection, $"PRAGMA user_version = {SchemaVersion}");
            }

            _ready = true;
        }
    }
}

/// <summary>
/// Who the data belongs to. There is no login yet, so every request acts as the one local user; a host that
/// adds authentication replaces this service with one that reads the user from the request, and the stores
/// need no change since they take the user's id on every call.
/// </summary>
public interface ICurrentUser
{
    string Id { get; }
}

/// <summary>The one user of an application without a login.</summary>
public sealed class LocalUser : ICurrentUser
{
    public string Id => Users.Local;
}

public static class Users
{
    /// <summary>The id of the user everything belongs to until there is a login; created with the schema.</summary>
    public const string Local = "local";

    public const string LocalName = "Local user";
}
