namespace Jellyfin.Plugin.RemoveSeries.Models;

public sealed class ExclusionDocument
{
    // Legacy versions stored either series or episode IDs here. Matching both forms
    // during filtering allows an in-place migration without losing user choices.
    public HashSet<Guid> ContinueWatching { get; set; } = [];

    public HashSet<Guid> ContinueWatchingEpisodes { get; set; } = [];

    public HashSet<Guid> ContinueWatchingSeries { get; set; } = [];

    public Dictionary<Guid, Guid> ContinueWatchingAllowedEpisodes { get; set; } = [];

    // Kept only to migrate the short-lived 1.1.0 cutoff format.
    public Dictionary<Guid, DateTime> ContinueWatchingSeriesCutoffs { get; set; } = [];

    public HashSet<Guid> NextUp { get; set; } = [];

    public ExclusionDocument Clone() => new()
    {
        ContinueWatching = [.. ContinueWatching],
        ContinueWatchingEpisodes = [.. ContinueWatchingEpisodes],
        ContinueWatchingSeries = [.. ContinueWatchingSeries],
        ContinueWatchingAllowedEpisodes = new Dictionary<Guid, Guid>(ContinueWatchingAllowedEpisodes),
        ContinueWatchingSeriesCutoffs = new Dictionary<Guid, DateTime>(ContinueWatchingSeriesCutoffs),
        NextUp = [.. NextUp]
    };
}
