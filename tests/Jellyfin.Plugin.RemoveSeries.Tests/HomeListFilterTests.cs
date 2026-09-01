using System.Security.Claims;
using Jellyfin.Plugin.RemoveSeries.Models;
using Jellyfin.Plugin.RemoveSeries.Services;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.RemoveSeries.Tests;

public sealed class HomeListFilterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"remove-series-filter-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task SeriesExclusionHidesOldResumeEpisodesButKeepsReactivatedEpisode()
    {
        Guid userId = Guid.NewGuid();
        Guid hiddenSeries = Guid.NewGuid();
        Guid visibleSeries = Guid.NewGuid();
        ExclusionStore store = new(_directory, NullLogger<ExclusionStore>.Instance);
        Guid oldEpisode = Guid.NewGuid();
        Guid reactivatedEpisode = Guid.NewGuid();
        await store.AddContinueWatchingSeriesAsync(userId, hiddenSeries);
        await store.ReactivateAfterPlaybackStartAsync(userId, reactivatedEpisode, hiddenSeries);
        HomeListFilter filter = new(store, NullLogger<HomeListFilter>.Instance);
        QueryResult<BaseItemDto> queryResult = new(
            0,
            3,
            [
                new BaseItemDto { Id = oldEpisode, SeriesId = hiddenSeries },
                new BaseItemDto { Id = reactivatedEpisode, SeriesId = hiddenSeries },
                new BaseItemDto { Id = Guid.NewGuid(), SeriesId = visibleSeries }
            ]);
        (ActionExecutingContext executing, ActionExecutedContext executed) = CreateContexts(userId, "Items", "GetResumeItems", queryResult);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        QueryResult<BaseItemDto> filtered = Assert.IsType<QueryResult<BaseItemDto>>(Assert.IsType<ObjectResult>(executed.Result).Value);
        Assert.Equal(2, filtered.Items.Count);
        Assert.Contains(filtered.Items, item => item.SeriesId == hiddenSeries);
        Assert.Contains(filtered.Items, item => item.SeriesId == visibleSeries);
        Assert.Equal(2, filtered.TotalRecordCount);
    }

    [Fact]
    public async Task EpisodeExclusionHidesOnlyThatResumeEpisode()
    {
        Guid userId = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        Guid hiddenEpisode = Guid.NewGuid();
        Guid visibleEpisode = Guid.NewGuid();
        ExclusionStore store = new(_directory, NullLogger<ExclusionStore>.Instance);
        await store.AddContinueWatchingEpisodeAsync(userId, hiddenEpisode);
        HomeListFilter filter = new(store, NullLogger<HomeListFilter>.Instance);
        QueryResult<BaseItemDto> queryResult = new(0, 2,
        [
            new BaseItemDto { Id = hiddenEpisode, SeriesId = seriesId },
            new BaseItemDto { Id = visibleEpisode, SeriesId = seriesId }
        ]);
        (ActionExecutingContext executing, ActionExecutedContext executed) = CreateContexts(userId, "Items", "GetResumeItems", queryResult);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        QueryResult<BaseItemDto> filtered = Assert.IsType<QueryResult<BaseItemDto>>(Assert.IsType<ObjectResult>(executed.Result).Value);
        Assert.Single(filtered.Items);
        Assert.Equal(visibleEpisode, filtered.Items[0].Id);
    }

    [Fact]
    public async Task KeepsResumeResultsWhenSeriesIsExcludedOnlyFromNextUp()
    {
        Guid userId = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        ExclusionStore store = new(_directory, NullLogger<ExclusionStore>.Instance);
        await store.AddNextUpSeriesAsync(userId, seriesId);
        HomeListFilter filter = new(store, NullLogger<HomeListFilter>.Instance);
        QueryResult<BaseItemDto> queryResult = new(0, 1, [new BaseItemDto { Id = Guid.NewGuid(), SeriesId = seriesId }]);
        (ActionExecutingContext executing, ActionExecutedContext executed) = CreateContexts(userId, "Items", "GetResumeItems", queryResult);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        QueryResult<BaseItemDto> filtered = Assert.IsType<QueryResult<BaseItemDto>>(Assert.IsType<ObjectResult>(executed.Result).Value);
        Assert.Single(filtered.Items);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private static (ActionExecutingContext Executing, ActionExecutedContext Executed) CreateContexts(
        Guid userId,
        string controller,
        string action,
        QueryResult<BaseItemDto> result)
    {
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("Jellyfin-UserId", userId.ToString())], "test"))
        };
        RouteData routeData = new();
        routeData.Values["controller"] = controller;
        routeData.Values["action"] = action;
        ActionContext actionContext = new(httpContext, routeData, new ActionDescriptor());
        List<IFilterMetadata> filters = [];
        object controllerInstance = new();
        ActionExecutingContext executing = new(actionContext, filters, new Dictionary<string, object?>(), controllerInstance);
        ActionExecutedContext executed = new(actionContext, filters, controllerInstance)
        {
            Result = new ObjectResult(result)
        };
        return (executing, executed);
    }
}
