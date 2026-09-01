namespace Jellyfin.Plugin.RemoveSeries.Models;

public sealed class ExclusionDocument
{
    public HashSet<Guid> ContinueWatching { get; set; } = [];

    public HashSet<Guid> NextUp { get; set; } = [];

    public HashSet<Guid> For(ExclusionSurface surface) =>
        surface == ExclusionSurface.ContinueWatching ? ContinueWatching : NextUp;

    public ExclusionDocument Clone() => new()
    {
        ContinueWatching = [.. ContinueWatching],
        NextUp = [.. NextUp]
    };
}

