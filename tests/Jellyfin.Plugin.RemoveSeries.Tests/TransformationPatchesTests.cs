using Jellyfin.Plugin.RemoveSeries.Helpers;

namespace Jellyfin.Plugin.RemoveSeries.Tests;

public sealed class TransformationPatchesTests
{
    [Fact]
    public void AddsLoaderOnceDirectlyAfterHead()
    {
        PatchRequestPayload payload = new() { Contents = "<!doctype html><html><head><title>Jellyfin</title></head></html>" };

        string first = TransformationPatches.IndexHtml(payload);
        string second = TransformationPatches.IndexHtml(new PatchRequestPayload { Contents = first });

        Assert.Contains("<head><script data-remove-series-loader>", first, StringComparison.Ordinal);
        Assert.Contains("XMLHttpRequest.prototype.open", first, StringComparison.Ordinal);
        Assert.Contains("__removeSeriesXhrPatched", first, StringComparison.Ordinal);
        Assert.Equal(first, second);
    }

    [Fact]
    public void LeavesEmptyPayloadUntouched()
    {
        Assert.Equal(string.Empty, TransformationPatches.IndexHtml(new PatchRequestPayload()));
    }
}
