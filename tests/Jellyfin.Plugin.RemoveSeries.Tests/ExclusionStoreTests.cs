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
        Guid continueSeries = Guid.NewGuid();
        Guid nextUpSeries = Guid.NewGuid();
        ExclusionStore store = CreateStore();

        await store.AddAsync(userId, continueSeries, ExclusionSurface.ContinueWatching);
        await store.AddAsync(userId, nextUpSeries, ExclusionSurface.NextUp);

        ExclusionDocument persisted = await CreateStore().GetSnapshotAsync(userId);
        Assert.Contains(continueSeries, persisted.ContinueWatching);
        Assert.DoesNotContain(continueSeries, persisted.NextUp);
        Assert.Contains(nextUpSeries, persisted.NextUp);
        Assert.DoesNotContain(nextUpSeries, persisted.ContinueWatching);
    }

    [Fact]
    public async Task PlaybackStyleRemovalClearsBothSurfaces()
    {
        Guid userId = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        ExclusionStore store = CreateStore();
        await store.AddAsync(userId, seriesId, ExclusionSurface.ContinueWatching);
        await store.AddAsync(userId, seriesId, ExclusionSurface.NextUp);

        await store.RemoveAllAsync(userId, seriesId);

        ExclusionDocument result = await store.GetSnapshotAsync(userId);
        Assert.DoesNotContain(seriesId, result.ContinueWatching);
        Assert.DoesNotContain(seriesId, result.NextUp);
    }

    [Fact]
    public async Task UsersAreIsolated()
    {
        Guid firstUser = Guid.NewGuid();
        Guid secondUser = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        ExclusionStore store = CreateStore();

        await store.AddAsync(firstUser, seriesId, ExclusionSurface.NextUp);

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

