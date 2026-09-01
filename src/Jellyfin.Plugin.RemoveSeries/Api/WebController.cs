using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RemoveSeries.Api;

[ApiController]
[Route("RemoveSeries/Web")]
public sealed class WebController : ControllerBase
{
    [HttpGet("plugin.js")]
    [AllowAnonymous]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public IActionResult GetPluginScript()
    {
        Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Jellyfin.Plugin.RemoveSeries.Web.plugin.mjs");
        return stream is null ? NotFound() : File(stream, "application/javascript; charset=utf-8");
    }
}

