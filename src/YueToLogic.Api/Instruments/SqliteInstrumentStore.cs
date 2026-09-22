using Microsoft.Data.Sqlite;

namespace YueToLogic.Api.Instruments;

/// <summary>
/// The instruments in one SQLite file. The file and its tables are created on first use, so a server whose
/// data directory is not writable still starts and converts; only the instrument endpoints fail then.
/// </summary>
/// <remarks>
/// Every call opens its own connection; Microsoft.Data.Sqlite pools them, and SQLite's file lock serializes
/// the writers. The schema carries a version in <c>PRAGMA user_version</c> so that later changes can be
/// applied in order to a file an earlier release wrote.
/// </remarks>
public sealed class SqliteInstrumentStore : IInstrumentStore
{
    private const int SchemaVersion = 1;

    /// <summary>SQLITE_CONSTRAINT_UNIQUE: the extended result code of a violated UNIQUE constraint.</summary>
    private const int UniqueConstraintViolated = 2067;

    private readonly string _connectionString;
    private readonly Lock _gate = new();
    private bool _ready;

    public SqliteInstrumentStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = Path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
    }

    /// <summary>Where the file is, for the log and for tests.</summary>
    public string Path { get; }

    public IReadOnlyList<Instrument> List()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, port, channel FROM instruments ORDER BY name COLLATE NOCASE, id";
        using var reader = command.ExecuteReader();
        var instruments = new List<Instrument>();
        while (reader.Read())
        {
            instruments.Add(Read(reader));
        }

        return instruments;
    }

    public Instrument? Get(long id)
    {
        using var connection = Open();
        return Get(connection, id);
    }

    public Instrument Add(string name, string port, int channel)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO instruments (name, port, channel) VALUES ($name, $port, $channel);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$port", port);
        command.Parameters.AddWithValue("$channel", channel);
        try
        {
            var id = (long)command.ExecuteScalar()!;
            return new Instrument(id, name, port, channel);
        }
        catch (SqliteException exception) when (exception.SqliteExtendedErrorCode == UniqueConstraintViolated)
        {
            throw new DuplicateInstrumentNameException(name);
        }
    }

    public Instrument? Update(long id, string name, string port, int channel)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE instruments
            SET name = $name, port = $port, channel = $channel, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$port", port);
        command.Parameters.AddWithValue("$channel", channel);
        try
        {
            return command.ExecuteNonQuery() == 0 ? null : new Instrument(id, name, port, channel);
        }
        catch (SqliteException exception) when (exception.SqliteExtendedErrorCode == UniqueConstraintViolated)
        {
            throw new DuplicateInstrumentNameException(name);
        }
    }

    public bool Delete(long id)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        // Spelled out rather than left to ON DELETE CASCADE, so it holds even if the pragma were ever missed.
        Execute(connection, "DELETE FROM track_assignments WHERE instrument_id = $id", ("$id", id));
        var removed = Execute(connection, "DELETE FROM instruments WHERE id = $id", ("$id", id));
        transaction.Commit();
        return removed > 0;
    }

    public IReadOnlyDictionary<string, long> Assignments()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT track, instrument_id FROM track_assignments ORDER BY track";
        using var reader = command.ExecuteReader();
        var assignments = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            assignments[reader.GetString(0)] = reader.GetInt64(1);
        }

        return assignments;
    }

    public bool Assign(string track, long instrumentId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        if (Get(connection, instrumentId) is null)
        {
            return false;
        }

        Execute(
            connection,
            """
            INSERT INTO track_assignments (track, instrument_id) VALUES ($track, $instrument)
            ON CONFLICT (track) DO UPDATE SET instrument_id = excluded.instrument_id
            """,
            ("$track", track),
            ("$instrument", instrumentId));
        transaction.Commit();
        return true;
    }

    public void Unassign(string track)
    {
        using var connection = Open();
        Execute(connection, "DELETE FROM track_assignments WHERE track = $track", ("$track", track));
    }

    private SqliteConnection Open()
    {
        EnsureCreated();
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        Execute(connection, "PRAGMA foreign_keys = ON");
        return connection;
    }

    /// <summary>Creates the directory, the file and the tables the first time the store is used.</summary>
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

                // Later versions add their steps here, each guarded by `current < n`, before the version is set.
                Execute(connection, $"PRAGMA user_version = {SchemaVersion}");
            }

            _ready = true;
        }
    }

    private static Instrument? Get(SqliteConnection connection, long id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, port, channel FROM instruments WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    private static Instrument Read(SqliteDataReader reader) =>
        new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3));

    private static int Execute(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command.ExecuteNonQuery();
    }
}
