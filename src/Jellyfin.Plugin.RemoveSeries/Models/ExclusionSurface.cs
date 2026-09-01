namespace Jellyfin.Plugin.RemoveSeries.Models;

public enum ExclusionSurface
{
    ContinueWatching,
    NextUp
}

public static class ExclusionSurfaceParser
{
    public static bool TryParse(string? value, out ExclusionSurface surface)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "continue-watching":
                surface = ExclusionSurface.ContinueWatching;
                return true;
            case "next-up":
                surface = ExclusionSurface.NextUp;
                return true;
            default:
                surface = default;
                return false;
        }
    }

    public static string ToWireValue(this ExclusionSurface surface) =>
        surface == ExclusionSurface.ContinueWatching ? "continue-watching" : "next-up";
}

