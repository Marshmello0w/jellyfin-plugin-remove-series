using System.Reflection;
using System.Runtime.Loader;
using Jellyfin.Plugin.RemoveSeries.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.RemoveSeries.Services;

public sealed class FileTransformationHostedService : IHostedService
{
    private readonly ILogger<FileTransformationHostedService> _logger;

    public FileTransformationHostedService(ILogger<FileTransformationHostedService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Assembly? assembly = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .FirstOrDefault(candidate => candidate.FullName?.Contains("FileTransformation", StringComparison.OrdinalIgnoreCase) == true);
            Type? interfaceType = assembly?.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
            MethodInfo? registerMethod = interfaceType?.GetMethod("RegisterTransformation");
            if (registerMethod is null)
            {
                _logger.LogWarning("File Transformation was not found. Install it to enable the Remove Series web menu.");
                return Task.CompletedTask;
            }

            JObject payload = new()
            {
                ["id"] = Plugin.PluginId.ToString(),
                ["fileNamePattern"] = "index.html",
                ["callbackAssembly"] = GetType().Assembly.FullName,
                ["callbackClass"] = typeof(TransformationPatches).FullName,
                ["callbackMethod"] = nameof(TransformationPatches.IndexHtml)
            };
            registerMethod.Invoke(null, [payload]);
            _logger.LogInformation("Remove Series registered its Jellyfin Web transformation.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Remove Series could not register with File Transformation.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

