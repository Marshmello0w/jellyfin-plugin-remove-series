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

        if (_libraryManager.GetItemById(request.ItemId) is not Episode episode || episode.SeriesId == Guid.Empty)
        {
            return BadRequest("itemId must reference an episode with a series.");
        }

        await _store.AddAsync(userId.Value, episode.SeriesId, surface, cancellationToken).ConfigureAwait(false);
        string seriesName = _libraryManager.GetItemById(episode.SeriesId)?.Name ?? episode.Name;
        return Ok(new ExclusionResponse(episode.SeriesId, seriesName, surface.ToWireValue()));
    }

    [HttpDelete("{surface}/{seriesId:guid}")]
    public async Task<IActionResult> RemoveAsync(
        string surface,
        Guid seriesId,
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

        await _store.RemoveAsync(userId.Value, seriesId, parsedSurface, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}

public sealed record ExclusionRequest(Guid ItemId, string Surface);

public sealed record ExclusionResponse(Guid SeriesId, string SeriesName, string Surface);

