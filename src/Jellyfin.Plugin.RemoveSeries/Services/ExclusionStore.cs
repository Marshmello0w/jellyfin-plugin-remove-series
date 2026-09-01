using System.Collections.Concurrent;
using System.Text.Json;
using Jellyfin.Plugin.RemoveSeries.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RemoveSeries.Services;

public sealed class ExclusionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _rootPath;
    private readonly ILogger<ExclusionStore> _logger;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<Guid, ExclusionDocument> _cache = new();

    public ExclusionStore(ILogger<ExclusionStore> logger)
        : this(Plugin.Instance?.DataFolderPath ?? throw new InvalidOperationException("Plugin is not initialized."), logger)
    {
    }

    internal ExclusionStore(string rootPath, ILogger<ExclusionStore> logger)
    {
        _rootPath = rootPath;
        _logger = logger;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<ExclusionDocument> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(userId, out ExclusionDocument? cached))
        {
            return cached.Clone();
        }

        SemaphoreSlim gate = _locks.GetOrAdd(userId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_cache.TryGetValue(userId, out cached))
            {
                cached = await ReadAsync(userId, cancellationToken).ConfigureAwait(false);
                _cache[userId] = cached;
            }

            return cached.Clone();
        }
        finally
        {
            gate.Release();
        }
    }

    public Task AddContinueWatchingEpisodeAsync(Guid userId, Guid episodeId, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, document => document.ContinueWatching.Add(episodeId), cancellationToken);

    public Task RemoveContinueWatchingEpisodeAsync(Guid userId, Guid episodeId, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, document => document.ContinueWatching.Remove(episodeId), cancellationToken);

    public Task AddContinueWatchingSeriesCutoffAsync(
        Guid userId,
        Guid seriesId,
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            userId,
            document =>
            {
                DateTime normalized = cutoffUtc.ToUniversalTime();
                bool changed = !document.ContinueWatchingSeriesCutoffs.TryGetValue(seriesId, out DateTime current)
                    || current != normalized;
                document.ContinueWatchingSeriesCutoffs[seriesId] = normalized;
                return changed;
            },
            cancellationToken);

    public Task RemoveContinueWatchingSeriesCutoffAsync(Guid userId, Guid seriesId, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, document => document.ContinueWatchingSeriesCutoffs.Remove(seriesId), cancellationToken);

    public Task AddNextUpSeriesAsync(Guid userId, Guid seriesId, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, document => document.NextUp.Add(seriesId), cancellationToken);

    public Task RemoveNextUpSeriesAsync(Guid userId, Guid seriesId, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, document => document.NextUp.Remove(seriesId), cancellationToken);

    private async Task MutateAsync(
        Guid userId,
        Func<ExclusionDocument, bool> mutation,
        CancellationToken cancellationToken)
    {
        SemaphoreSlim gate = _locks.GetOrAdd(userId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ExclusionDocument document = _cache.TryGetValue(userId, out ExclusionDocument? cached)
                ? cached.Clone()
                : await ReadAsync(userId, cancellationToken).ConfigureAwait(false);

            if (!mutation(document))
            {
                _cache[userId] = document;
                return;
            }

            await WriteAsync(userId, document, cancellationToken).ConfigureAwait(false);
            _cache[userId] = document;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<ExclusionDocument> ReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        string path = GetPath(userId);
        if (!File.Exists(path))
        {
            return new ExclusionDocument();
        }

        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<ExclusionDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? new ExclusionDocument();
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Could not read exclusion data for user {UserId}; filtering is disabled for that user until repaired.", userId);
            return new ExclusionDocument();
        }
    }

    private async Task WriteAsync(Guid userId, ExclusionDocument document, CancellationToken cancellationToken)
    {
        string path = GetPath(userId);
        string temporaryPath = path + ".tmp";
        try
        {
            await using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private string GetPath(Guid userId) => Path.Combine(_rootPath, $"{userId:N}.json");
}
