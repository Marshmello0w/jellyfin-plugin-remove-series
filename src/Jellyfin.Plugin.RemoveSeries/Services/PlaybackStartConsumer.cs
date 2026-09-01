using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RemoveSeries.Services;

public sealed class PlaybackStartConsumer : IEventConsumer<PlaybackStartEventArgs>
{
    private readonly ExclusionStore _store;
    private readonly ILogger<PlaybackStartConsumer> _logger;

    public PlaybackStartConsumer(ExclusionStore store, ILogger<PlaybackStartConsumer> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task OnEvent(PlaybackStartEventArgs eventArgs)
    {
        if (eventArgs.Item is not Episode episode
            || episode.SeriesId == Guid.Empty
            || eventArgs.Session?.UserId is not Guid userId
            || userId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _store.ReactivateAfterPlaybackStartAsync(userId, episode.Id, episode.SeriesId).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not reactivate episode {EpisodeId} for user {UserId} after playback started.", episode.Id, userId);
        }
    }
}
