using Jellyfin.Plugin.RemoveSeries.Helpers;
using Jellyfin.Plugin.RemoveSeries.Models;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RemoveSeries.Services;

public sealed class HomeListFilter : IAsyncActionFilter
{
    private static readonly Dictionary<(string Controller, string Action), ExclusionSurface> Routes =
        new(new RouteKeyComparer())
        {
            [("Items", "GetResumeItems")] = ExclusionSurface.ContinueWatching,
            [("Items", "GetResumeItemsLegacy")] = ExclusionSurface.ContinueWatching,
            [("TvShows", "GetNextUp")] = ExclusionSurface.NextUp
        };

    private readonly ExclusionStore _store;
    private readonly ILogger<HomeListFilter> _logger;

    public HomeListFilter(ExclusionStore store, ILogger<HomeListFilter> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!TryGetSurface(context, out ExclusionSurface surface))
        {
            await next().ConfigureAwait(false);
            return;
        }

        Guid? userId = CurrentUser.GetId(context.HttpContext.User);
        if (!userId.HasValue)
        {
            await next().ConfigureAwait(false);
            return;
        }

        ExclusionDocument snapshot = await _store.GetSnapshotAsync(userId.Value, context.HttpContext.RequestAborted).ConfigureAwait(false);
        HashSet<Guid> excluded = snapshot.For(surface);
        if (excluded.Count == 0)
        {
            await next().ConfigureAwait(false);
            return;
        }

        ActionExecutedContext executed = await next().ConfigureAwait(false);
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return;
        }

        if (executed.Result is not ObjectResult objectResult || objectResult.Value is not QueryResult<BaseItemDto> result)
        {
            _logger.LogWarning("Remove Series could not filter {Controller}/{Action}: unexpected response type {ResponseType}.",
                context.RouteData.Values["controller"],
                context.RouteData.Values["action"],
                executed.Result?.GetType().FullName ?? "null");
            return;
        }

        List<BaseItemDto> kept = result.Items
            .Where(item => !item.SeriesId.HasValue || !excluded.Contains(item.SeriesId.Value))
            .ToList();
        int removed = result.Items.Count - kept.Count;
        if (removed == 0)
        {
            return;
        }

        objectResult.Value = new QueryResult<BaseItemDto>(
            result.StartIndex,
            Math.Max(0, result.TotalRecordCount - removed),
            kept);
    }

    internal static bool TryGetSurface(ActionExecutingContext context, out ExclusionSurface surface)
    {
        surface = default;
        object? controller = context.RouteData.Values.GetValueOrDefault("controller");
        object? action = context.RouteData.Values.GetValueOrDefault("action");
        return controller is string controllerName
            && action is string actionName
            && Routes.TryGetValue((controllerName, actionName), out surface);
    }

    private sealed class RouteKeyComparer : IEqualityComparer<(string Controller, string Action)>
    {
        public bool Equals((string Controller, string Action) x, (string Controller, string Action) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Controller, y.Controller)
            && StringComparer.OrdinalIgnoreCase.Equals(x.Action, y.Action);

        public int GetHashCode((string Controller, string Action) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Controller),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Action));
    }
}
