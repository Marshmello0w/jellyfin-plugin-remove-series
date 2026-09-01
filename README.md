# Remove Series for Jellyfin

`Remove Series` adds non-destructive actions to Jellyfin's **Continue Watching** and **Next Up** rows. A user can hide one episode or the current entries of a series without changing watched flags or playback positions.

## Features

- **Remove episode from Continue Watching** hides only the selected episode.
- **Remove series from Continue Watching** hides the episodes that were already present at that moment. Episodes played afterward can appear normally without restoring the older entries.
- **Remove series from Next Up** remains series-wide.
- Works from right-click, the three-dot action menu, and touch/long-press in Jellyfin Web.
- Hides every episode of that series from the selected row.
- Stores exclusions per user on the Jellyfin server, so the result also applies to native clients.
- Shows a confirmation and an eight-second Undo action.
- Starting an individually hidden episode makes only that episode eligible again. Starting an episode also re-enables the series in Next Up.
- German and English web-interface text.

## Requirements

- Jellyfin Server 10.11.x
- [File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation)

The menu integration modifies Jellyfin Web only. Once an exclusion has been created, the server-side filtering also affects other Jellyfin clients for that user.

## Installation through the plugin catalog

1. Open **Dashboard → Plugins → Repositories**.
2. Add the File Transformation repository:

   ```text
   https://www.iamparadox.dev/jellyfin/plugins/manifest.json
   ```

3. Add the Remove Series repository:

   ```text
   https://raw.githubusercontent.com/Marshmello0w/jellyfin-plugin-remove-series/refs/heads/main/manifest.json
   ```

4. Install **File Transformation**, then install **Remove Series**.
5. Restart Jellyfin and hard-refresh Jellyfin Web once.

## Usage

Open the action menu of an episode card in **Continue Watching** or **Next Up**, choose the episode or series action, and confirm. Only the selected home row is changed. Playback progress and watched state remain exactly as they were.

If removal was accidental, select **Undo** in the confirmation toast. Playing any episode from the series detail page also makes the series eligible for both rows again.

## Privacy and data

The plugin stores only Jellyfin user IDs and series IDs in its own data directory. It never writes to Jellyfin user data, playback positions, watched flags, media metadata, or media files.

## Development

```powershell
dotnet build Jellyfin.Plugin.RemoveSeries.slnx --configuration Release
dotnet test Jellyfin.Plugin.RemoveSeries.slnx --configuration Release
npm test
```

The release workflow packages the plugin DLL, publishes a GitHub Release, computes the MD5 expected by Jellyfin, and prepends the release to `manifest.json`.

## License

GPL-3.0-only. See [LICENSE](LICENSE).
