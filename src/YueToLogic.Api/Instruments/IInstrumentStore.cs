namespace YueToLogic.Api.Instruments;

/// <summary>
/// Keeps the instruments and which track plays which. The methods are synchronous because the store is a
/// local file whose queries take microseconds; an implementation over a network would change that.
/// </summary>
public interface IInstrumentStore
{
    /// <summary>All instruments, ordered by name.</summary>
    IReadOnlyList<Instrument> List();

    Instrument? Get(long id);

    /// <exception cref="DuplicateInstrumentNameException">An instrument of that name exists already (case-insensitively).</exception>
    Instrument Add(string name, string port, int channel);

    /// <returns>The changed instrument, or <c>null</c> when there is none with that id.</returns>
    /// <exception cref="DuplicateInstrumentNameException">Another instrument has that name already.</exception>
    Instrument? Update(long id, string name, string port, int channel);

    /// <summary>Removes the instrument and every assignment of a track to it.</summary>
    /// <returns>Whether there was an instrument with that id.</returns>
    bool Delete(long id);

    /// <summary>Track name → instrument id, for every track that has an instrument.</summary>
    IReadOnlyDictionary<string, long> Assignments();

    /// <returns><c>false</c> when there is no instrument with that id; nothing is stored then.</returns>
    bool Assign(string track, long instrumentId);

    void Unassign(string track);
}

public sealed class DuplicateInstrumentNameException(string name)
    : Exception($"An instrument named '{name}' exists already.")
{
    public string Name { get; } = name;
}
