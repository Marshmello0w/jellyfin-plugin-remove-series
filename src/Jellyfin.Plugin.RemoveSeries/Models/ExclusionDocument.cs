namespace Jellyfin.Plugin.RemoveSeries.Models;

public sealed class ExclusionDocument
{
    // Version 1.0 stored series IDs here. Version 1.1 treats this set as episode IDs;
    // legacy series IDs are harmless because they never match an episode ID.
    public HashSet<Guid> ContinueWatching { get; set; } = [];

    public Dictionary<Guid, DateTime> ContinueWatchingSeriesCutoffs { get; set; } = [];

    public HashSet<Guid> NextUp { get; set; } = [];

    public ExclusionDocument Clone() => new()
    {
        ContinueWatching = [.. ContinueWatching],
        ContinueWatchingSeriesCutoffs = new Dictionary<Guid, DateTime>(ContinueWatchingSeriesCutoffs),
        NextUp = [.. NextUp]
    };
}
