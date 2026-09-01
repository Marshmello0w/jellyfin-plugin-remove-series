using Jellyfin.Plugin.RemoveSeries.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.RemoveSeries;

public sealed class Plugin : BasePlugin<PluginConfiguration>
{
    public static readonly Guid PluginId = Guid.Parse("bd12e7ab-7f62-42c2-8eb2-41f59f24d117");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Remove Series";

    public override string Description => "Removes a series from Continue Watching or Next Up without changing playback progress.";

    public override Guid Id => PluginId;
}

