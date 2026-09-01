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

        const string loader = "<script data-remove-series-loader>(function(){var f=window.fetch,q=[];window.__removeSeriesEarly=q;window.fetch=async function(i,o){var r=await f.apply(this,arguments),u=String(typeof i==='string'?i:(i&&i.url)||'').toLowerCase(),s=/\\/(?:users\\/[^/]+\\/)?items\\/resume(?:[?#]|$)|\\/useritems\\/resume(?:[?#]|$)/.test(u)?'continue-watching':/\\/shows\\/nextup(?:[?#]|$)|\\/nextup(?:[?#]|$)/.test(u)?'next-up':null;if(s&&r.ok)r.clone().json().then(function(j){var h=window.__removeSeriesCaptureHandler;h?h(j,s):q.push([j,s]);}).catch(function(){});return r;};window.__removeSeriesFetchPatched=true;var e=document.createElement('script');e.type='module';e.src=new URL('../RemoveSeries/Web/plugin.js?v=1.1.0.0',document.baseURI).href;document.head.appendChild(e);}());</script>";
        return HeadRegex().Replace(contents, $"$1{loader}", 1);
    }

    [GeneratedRegex("(<head[^>]*>)", RegexOptions.IgnoreCase)]
    private static partial Regex HeadRegex();
}
