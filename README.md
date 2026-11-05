# SignalSort

SignalSort identifies what your files **actually are**, then gives them
names that make sense.

It is a lightweight, offline-first Windows desktop utility (WPF, .NET 8) that
scans a folder, infers what each file is from technical characteristics and
embedded metadata — not just its extension — and generates safe, meaningful
renames that you preview before applying.

> SignalSort tells you what your files are, not just what their extensions say.

## What it does

1. **Scan** a folder (optionally recursively).
2. **Classify** every file and folder using technical signals: audio/video
   duration, bitrate, channels, resolution, image dimensions and EXIF date
   taken, plus folder contents.
3. **Preview** the original name next to the suggested new name, with the
   evidence behind each classification.
4. **Rename** only what you select, after pre-flight collision detection, with
   a persistent journal so any batch can be undone.

## Technical classification (offline, deterministic)

| Kind | Signals used | Example purposes |
|------|--------------|------------------|
| Audio | Duration, bitrate, channels | `VoiceMemo` (mono + low bitrate), `SoundEcho` (<10s), `Stinger` (10–45s), `Song` (>45s) |
| Video | Resolution, duration | `ScreenRec` (<720p), `CameraFootage` (≥1080p), `Clip` (<15s), `Short` (15–120s), `Recording` (>120s) |
| Image | Dimensions, EXIF date taken | `Icon` (≤64 px), `Thumbnail` (≤256 px), `Photo` (>256 px) |
| Archive | Extension | `Archive` |
| Folder | Contents mix | `MediaDump`, `AudioCollection`, `VideoSet`, `ImagePack`, `Archive_YYYY-MM-DD`, `Empty` |
| Document | Extension | `Document` |

Every classification carries a confidence level (`strong`, `heuristic`,
`uncertain`) so you can tell when the app is certain and when it is guessing.

## Naming

The default style is **category-prefixed**, sortable and collision-safe:

```
VoiceMemo_20261104_001.m4a
ScreenRec_20261104_002.mp4
Photo_20260115_001.jpg
Song_001.mp3
```

Alternate strategies are available: category + number, category + hash,
category + timestamp, and category + adjective+noun.

## Safety

- Full **preview before execution** — nothing is renamed until you confirm.
- **Collision detection** for the whole batch, including case-insensitive
  clashes and rename chains; cycles are detected and blocked.
- **No silent overwrites** — `File.Move` never overwrites.
- **One-click undo** for the last completed batch, with a persistent journal
  stored under `%LOCALAPPDATA%\SignalSort`.
- Undo verifies each file (size + timestamp fingerprint) before reversing and
  skips files that were modified after the rename.

## Privacy

Processing is entirely **offline and local**. No file content is uploaded, no
telemetry is sent, and no account is required. Settings and the rename journal
live in `%LOCALAPPDATA%\SignalSort`; nothing is written into the folders
you scan.

## Installation

### Released build

SignalSort publishes as a **single-file, self-contained Windows executable**
(`.exe`, ~70 MB) that includes the .NET runtime. Download it, double-click it,
and run — no installation and no .NET SDK required.

### Build from source

Requires the .NET 8.0 SDK and Windows 10/11.

```powershell
# Clone or unpack the source, then:
dotnet build SignalSort.sln -c Release
dotnet publish src/SignalSort/SignalSort.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output lands in
`src/SignalSort/bin/Release/net8.0-windows/win-x64/publish/`.

## Continuous integration

`build-and-test` runs on `windows-latest` via GitHub Actions:
checkout → setup .NET 8 SDK → restore → build Release → run tests.

## Known limitations

- **Windows only** — WPF is Windows-specific.
- **Audio/video classification needs readable media files.** Corrupt or
  unsupported containers fall back to `UnknownAudio`/`UnknownVideo`.
- **No AI naming** — classification is heuristic and deterministic; there is
  no LLM or online lookup (by design, to stay offline and predictable).
- **No automatic updates** — check for new releases manually.
- Undo reverses the **most recent completed batch** only.

## Licensing

No license has been selected for this project yet. Licensing is an explicit
decision left for before any public release.
