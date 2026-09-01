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
        DateTime cutoff = DateTime.UtcNow;
        ExclusionStore store = CreateStore();

        await store.AddContinueWatchingEpisodeAsync(userId, continueEpisode);
        await store.AddContinueWatchingSeriesCutoffAsync(userId, continueSeries, cutoff);
        await store.AddNextUpSeriesAsync(userId, nextUpSeries);

        ExclusionDocument persisted = await CreateStore().GetSnapshotAsync(userId);
        Assert.Contains(continueEpisode, persisted.ContinueWatching);
        Assert.Equal(cutoff, persisted.ContinueWatchingSeriesCutoffs[continueSeries]);
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
        await store.AddContinueWatchingSeriesCutoffAsync(userId, seriesId, DateTime.UtcNow);
        await store.AddNextUpSeriesAsync(userId, seriesId);

        await store.RemoveContinueWatchingEpisodeAsync(userId, startedEpisode);
        await store.RemoveNextUpSeriesAsync(userId, seriesId);

        ExclusionDocument result = await store.GetSnapshotAsync(userId);
        Assert.DoesNotContain(startedEpisode, result.ContinueWatching);
        Assert.Contains(otherEpisode, result.ContinueWatching);
        Assert.Contains(seriesId, result.ContinueWatchingSeriesCutoffs.Keys);
        Assert.DoesNotContain(seriesId, result.NextUp);
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
