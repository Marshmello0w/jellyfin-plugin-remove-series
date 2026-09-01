using Jellyfin.Plugin.RemoveSeries.Helpers;
using Jellyfin.Plugin.RemoveSeries.Models;
using Jellyfin.Plugin.RemoveSeries.Services;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RemoveSeries.Api;

[ApiController]
[Authorize]
[Route("RemoveSeries/Exclusions")]
public sealed class ExclusionsController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly ExclusionStore _store;

    public ExclusionsController(ILibraryManager libraryManager, ExclusionStore store)
    {
        _libraryManager = libraryManager;
        _store = store;
    }

    [HttpPost]
    public async Task<ActionResult<ExclusionResponse>> AddAsync(
        [FromBody] ExclusionRequest request,
        CancellationToken cancellationToken)
    {
        Guid? userId = CurrentUser.GetId(User);
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        if (!ExclusionSurfaceParser.TryParse(request.Surface, out ExclusionSurface surface))
        {
            return BadRequest("surface must be 'continue-watching' or 'next-up'.");
        }

        if (request.ItemId == Guid.Empty
            || _libraryManager.GetItemById(request.ItemId) is not Episode episode
            || episode.SeriesId == Guid.Empty)
        {
            return BadRequest("itemId must reference an episode with a series.");
        }

        string mode = request.Mode?.Trim().ToLowerInvariant() ?? "series";
        if (mode is not ("series" or "episode"))
        {
            return BadRequest("mode must be 'series' or 'episode'.");
        }

        if (surface == ExclusionSurface.NextUp && mode == "episode")
        {
            return BadRequest("next-up exclusions can only target a series.");
        }

        Guid targetId;
        if (surface == ExclusionSurface.ContinueWatching && mode == "episode")
        {
            targetId = episode.Id;
            await _store.AddContinueWatchingEpisodeAsync(userId.Value, targetId, cancellationToken).ConfigureAwait(false);
        }
        else if (surface == ExclusionSurface.ContinueWatching)
        {
            targetId = episode.SeriesId;
            await _store.AddContinueWatchingSeriesAsync(userId.Value, targetId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            targetId = episode.SeriesId;
            await _store.AddNextUpSeriesAsync(userId.Value, targetId, cancellationToken).ConfigureAwait(false);
        }

        string seriesName = _libraryManager.GetItemById(episode.SeriesId)?.Name ?? episode.Name;
        return Ok(new ExclusionResponse(targetId, episode.SeriesId, seriesName, episode.Name, surface.ToWireValue(), mode));
    }

    [Obsolete("Use the mode-aware endpoint.")]
    [HttpDelete("{surface}/{seriesId:guid}")]
    public Task<IActionResult> RemoveLegacyAsync(
        string surface,
        Guid seriesId,
        CancellationToken cancellationToken) =>
        RemoveAsync(surface, "series", seriesId, cancellationToken);

    [HttpDelete("{surface}/{mode}/{targetId:guid}")]
    public async Task<IActionResult> RemoveAsync(
        string surface,
        string mode,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        Guid? userId = CurrentUser.GetId(User);
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        if (!ExclusionSurfaceParser.TryParse(surface, out ExclusionSurface parsedSurface))
        {
            return BadRequest("surface must be 'continue-watching' or 'next-up'.");
        }

        string normalizedMode = mode.Trim().ToLowerInvariant();
        if (normalizedMode is not ("series" or "episode"))
        {
            return BadRequest("mode must be 'series' or 'episode'.");
        }

        if (parsedSurface == ExclusionSurface.NextUp && normalizedMode == "episode")
        {
            return BadRequest("next-up exclusions can only target a series.");
        }

        if (parsedSurface == ExclusionSurface.ContinueWatching && normalizedMode == "episode")
        {
            await _store.RemoveContinueWatchingEpisodeAsync(userId.Value, targetId, cancellationToken).ConfigureAwait(false);
        }
        else if (parsedSurface == ExclusionSurface.ContinueWatching)
        {
            await _store.RemoveContinueWatchingSeriesAsync(userId.Value, targetId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _store.RemoveNextUpSeriesAsync(userId.Value, targetId, cancellationToken).ConfigureAwait(false);
        }

        return NoContent();
    }
}

public sealed record ExclusionRequest(Guid ItemId, string Surface, string? Mode = null);

public sealed record ExclusionResponse(
    Guid TargetId,
    Guid SeriesId,
    string SeriesName,
    string EpisodeName,
    string Surface,
    string Mode);
