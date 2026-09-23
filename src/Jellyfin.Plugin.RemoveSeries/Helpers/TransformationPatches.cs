using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.RemoveSeries.Helpers;

public static partial class TransformationPatches
{
    private const string Marker = "data-remove-series-loader";

    public static string IndexHtml(PatchRequestPayload payload)
    {
        string contents = payload.Contents ?? string.Empty;
        if (contents.Length == 0 || contents.Contains(Marker, StringComparison.Ordinal))
        {
            return contents;
        }

        const string loader = """
            <script data-remove-series-loader>(function(){
                var queued = window.__removeSeriesEarly = [];
                function surfaceFor(url) {
                    var path;
                    try { path = new URL(String(url || ''), document.baseURI).pathname.toLowerCase(); }
                    catch (_) { return null; }
                    if (/\/(?:users\/[^/]+\/)?items\/resume$|\/useritems\/resume$/.test(path)) return 'continue-watching';
                    if (/\/shows\/nextup$|\/nextup$/.test(path)) return 'next-up';
                    return null;
                }
                function capture(payload, surface) {
                    var handler = window.__removeSeriesCaptureHandler;
                    if (handler) handler(payload, surface); else queued.push([payload, surface]);
                }
                var originalFetch = window.fetch;
                window.fetch = async function(input) {
                    var response = await originalFetch.apply(this, arguments);
                    var surface = surfaceFor(typeof input === 'string' ? input : input && input.url);
                    if (surface && response.ok) response.clone().json().then(function(payload) {
                        capture(payload, surface);
                    }).catch(function() {});
                    return response;
                };
                window.__removeSeriesFetchPatched = true;
                var originalOpen = XMLHttpRequest.prototype.open;
                var originalSend = XMLHttpRequest.prototype.send;
                var surfaces = new WeakMap();
                XMLHttpRequest.prototype.open = function(method, url) {
                    surfaces.set(this, surfaceFor(url));
                    return originalOpen.apply(this, arguments);
                };
                XMLHttpRequest.prototype.send = function() {
                    var xhr = this, surface = surfaces.get(xhr);
                    if (surface) xhr.addEventListener('load', function() {
                        if (xhr.status < 200 || xhr.status >= 300) return;
                        try {
                            capture(typeof xhr.response === 'string' ? JSON.parse(xhr.response) : xhr.response, surface);
                        } catch (_) {}
                    }, { once: true });
                    return originalSend.apply(this, arguments);
                };
                window.__removeSeriesXhrPatched = true;
                var script = document.createElement('script');
                script.type = 'module';
                script.src = new URL('../RemoveSeries/Web/plugin.js?v=1.2.0.0', document.baseURI).href;
                document.head.appendChild(script);
            }());</script>
            """;
        return HeadRegex().Replace(contents, $"$1{loader}", 1);
    }

    [GeneratedRegex("(<head[^>]*>)", RegexOptions.IgnoreCase)]
    private static partial Regex HeadRegex();
}
