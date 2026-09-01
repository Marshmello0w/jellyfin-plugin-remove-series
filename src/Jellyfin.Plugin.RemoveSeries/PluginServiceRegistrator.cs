using Jellyfin.Plugin.RemoveSeries.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.RemoveSeries;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ExclusionStore>();
        serviceCollection.AddSingleton<HomeListFilter>();
        serviceCollection.AddScoped<IEventConsumer<PlaybackStartEventArgs>, PlaybackStartConsumer>();
        serviceCollection.AddHostedService<FileTransformationHostedService>();
        serviceCollection.Configure<MvcOptions>(options => options.Filters.AddService<HomeListFilter>());
    }
}
