using Jellyfin.Plugin.RemoveSeries.Models;
using Jellyfin.Plugin.RemoveSeries.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.RemoveSeries.Tests;

public sealed class ExclusionStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"remove-series-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task StoresSurfacesIndependentlyAndPersistsThem()
    {
        Guid userId = Guid.NewGuid();
        Guid continueEpisode = Guid.NewGuid();
        Guid continueSeries = Guid.NewGuid();
        Guid nextUpSeries = Guid.NewGuid();
        ExclusionStore store = CreateStore();

        await store.AddContinueWatchingEpisodeAsync(userId, continueEpisode);
        await store.AddContinueWatchingSeriesAsync(userId, continueSeries);
        await store.AddNextUpSeriesAsync(userId, nextUpSeries);

        ExclusionDocument persisted = await CreateStore().GetSnapshotAsync(userId);
        Assert.Contains(continueEpisode, persisted.ContinueWatchingEpisodes);
        Assert.Contains(continueSeries, persisted.ContinueWatchingSeries);
        Assert.Contains(nextUpSeries, persisted.NextUp);
    }

    [Fact]
    public async Task PlaybackStyleRemovalClearsOnlyStartedEpisodeAndNextUpSeries()
    {
        Guid userId = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        Guid startedEpisode = Guid.NewGuid();
        Guid otherEpisode = Guid.NewGuid();
        ExclusionStore store = CreateStore();
        await store.AddContinueWatchingEpisodeAsync(userId, startedEpisode);
        await store.AddContinueWatchingEpisodeAsync(userId, otherEpisode);
        await store.AddContinueWatchingSeriesAsync(userId, seriesId);
        await store.AddNextUpSeriesAsync(userId, seriesId);

        await store.ReactivateAfterPlaybackStartAsync(userId, startedEpisode, seriesId);

        ExclusionDocument result = await store.GetSnapshotAsync(userId);
        Assert.DoesNotContain(startedEpisode, result.ContinueWatchingEpisodes);
        Assert.Contains(otherEpisode, result.ContinueWatchingEpisodes);
        Assert.Contains(seriesId, result.ContinueWatchingSeries);
        Assert.Equal(seriesId, result.ContinueWatchingAllowedEpisodes[startedEpisode]);
        Assert.DoesNotContain(seriesId, result.NextUp);
    }

    [Fact]
    public async Task PlaybackMigratesLegacySeriesExclusionAndAllowsOnlyStartedEpisode()
    {
        Guid userId = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        Guid startedEpisode = Guid.NewGuid();
        ExclusionStore store = CreateStore();
        await File.WriteAllTextAsync(
            Path.Combine(_directory, $"{userId:N}.json"),
            $$"""{"continueWatching":["{{seriesId}}"],"nextUp":[]}""");

        await store.ReactivateAfterPlaybackStartAsync(userId, startedEpisode, seriesId);

        ExclusionDocument result = await store.GetSnapshotAsync(userId);
        Assert.DoesNotContain(seriesId, result.ContinueWatching);
        Assert.Contains(seriesId, result.ContinueWatchingSeries);
        Assert.Equal(seriesId, result.ContinueWatchingAllowedEpisodes[startedEpisode]);
    }

    [Fact]
    public async Task UsersAreIsolated()
    {
        Guid firstUser = Guid.NewGuid();
        Guid secondUser = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        ExclusionStore store = CreateStore();

        await store.AddNextUpSeriesAsync(firstUser, seriesId);

        Assert.Contains(seriesId, (await store.GetSnapshotAsync(firstUser)).NextUp);
        Assert.Empty((await store.GetSnapshotAsync(secondUser)).NextUp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private ExclusionStore CreateStore() => new(_directory, NullLogger<ExclusionStore>.Instance);
}
