# gmsb-codec-ogg

OGG Vorbis playback codec plugin for
[Game Master Sound Board](https://github.com/DevinSanders/game-master-soundboard).

Adds `.ogg` / `.oga` support via the pure-managed
[NVorbis](https://github.com/NVorbis/NVorbis) decoder.

## Install

Drop the released `.zip` onto Settings → Plugin Manager. Restart, then
enable **OGG Vorbis Codec** under Settings → Plugins.

Pre-built zips are attached to each [GitHub Release](../../releases).

## Build

Requires .NET 10 SDK. `SoundBoard.PluginApi` is restored from NuGet
automatically — no sibling checkout needed.

```powershell
dotnet build src/OggCodecPlugin.csproj
pwsh scripts/package.ps1
```

## Manifest

| Field     | Value                       |
|-----------|-----------------------------|
| publisher | `github.DevinSanders`       |
| id        | `codec.ogg`                 |
| entryDll  | `OggCodecPlugin.dll`        |

## License

Released under the [MIT License](LICENSE).

Third-party components used by this plugin:

- NVorbis (MIT) for pure-managed OGG Vorbis decoding.