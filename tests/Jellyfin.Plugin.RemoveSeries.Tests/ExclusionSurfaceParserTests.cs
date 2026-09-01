using Jellyfin.Plugin.RemoveSeries.Models;

namespace Jellyfin.Plugin.RemoveSeries.Tests;

public sealed class ExclusionSurfaceParserTests
{
    [Theory]
    [InlineData("continue-watching", ExclusionSurface.ContinueWatching)]
    [InlineData("CONTINUE-WATCHING", ExclusionSurface.ContinueWatching)]
    [InlineData("next-up", ExclusionSurface.NextUp)]
    public void AcceptsOnlyWireValues(string value, ExclusionSurface expected)
    {
        Assert.True(ExclusionSurfaceParser.TryParse(value, out ExclusionSurface actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("both")]
    [InlineData("continuewatching")]
    public void RejectsUnknownValues(string value)
    {
        Assert.False(ExclusionSurfaceParser.TryParse(value, out _));
    }
}

