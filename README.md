![SignalSort social preview](https://repository-images.githubusercontent.com/1448843297/407adb7d-36db-472e-b9ae-d4faaeec0349)

SignalSort analyzes technical characteristics and embedded metadata to identify
what files actually are, then generates meaningful filenames and lets you
rename them safely.

I made this because when I download a bunch of files, I tend to have trouble sorting the hundreds of files that I have. Audio, images, or something along the lines like that. I was very disorganized, and using AI, though helpful, can be a waste of resources in the long run, so something running manually on your computer can save on resources. I know this might seem useless for most people, but this has helped me with my file management!

A rename tool can insert dates and counters, but only after *someone* decides
what a file is. SignalSort makes that decision for you, from evidence inside
the file: duration, bitrate, channel count, resolution, image dimensions and
EXIF capture dates. A mono 8 kbps recording becomes `VoiceMemo_...`; a
640×360 screen capture becomes `ScreenRec_...`; a dated folder of exports
becomes `Archive_2026-01-15`.

## Features

- **Content-based classification** — audio, video, images, archives,
  documents and folders, each with a stated confidence level
  (`strong match` / `heuristic` / `uncertain`) and the evidence behind it.
- **Category-prefixed names by default** — `VoiceMemo_20261104_001.m4a`,
  `ScreenRec_20261104_002.mp4`, `Photo_20260115_001.jpg` — sortable,
  searchable, collision-free by construction.
- **Alternate naming styles** — category + number, + hash, + timestamp,
  + adjective-noun.
- **Full preview before anything happens** — original name next to the
  proposed name, with per-file evidence in the inspector.
- **Batch collision detection** — case-insensitive clashes, duplicate
  destinations, rename chains and cycles are detected before execution;
  nested folder/file batches are ordered so nothing invalidates anything.
- **One-click undo** — every rename batch is journaled; undo verifies each
  file (size + timestamp) before reversing and skips anything modified since.
- **Fast and responsive** — background scanning with progress and
  cancellation; the UI never blocks.
- **Dark and light themes** built around the Signal Rail design language.

## How classification works

SignalSort never trusts the extension alone. For every item it reads technical
characteristics with [TagLibSharp](https://github.com/mono/taglib-sharp) and
applies deterministic, offline rules:

| Kind | Signals read | Example purposes |
|------|--------------|------------------|
| Audio | duration, bitrate, channels | `VoiceMemo` (mono + low bitrate), `SoundEffect` (<10 s), `Stinger` (10–45 s), `Song` |
| Video | resolution, duration | `ScreenRec` (<720p), `CameraFootage` (≥1080p), `Clip` (<15 s), `Short`, `Recording` |
| Image | dimensions, EXIF date taken | `Icon` (≤64 px), `Thumbnail` (≤256 px), `Photo` |
| Folder | content mix | `MediaDump`, `AudioCollection`, `VideoSet`, `ImagePack`, `Archive_YYYY-MM-DD` |
| Archive / Document | extension | `Archive`, `Document` |

Heuristics can be wrong — SignalSort says so. Every row shows how confident
the match is, and the inspector lets you correct the purpose and regenerate
the name.

## Rename safety

1. **Preview** writes the full plan (including anything blocked) to the log.
2. **Rename** runs only planner-approved entries, in dependency-safe order,
   re-checking the disk immediately before every move. `File.Move` never
   overwrites.
3. **Journal** — each success is written immediately to
   `%LOCALAPPDATA%\SignalSort\journal.json`, so even a crash mid-batch leaves
   a complete record.
4. **Undo** reverses the most recent batch — including partially failed or
   interrupted ones — deepest-path-first, skipping files that changed since.

## Privacy

SignalSort works entirely offline by default. No file contents, names or
telemetry ever leave your machine. The only network call the app can make is
an update check you trigger yourself, to an HTTPS manifest URL you configure
in Settings — it is disabled (empty) out of the box, and updates are never
applied automatically (see [`docs/public/UPDATE_SYSTEM.md`](docs/public/UPDATE_SYSTEM.md)).
Settings and the rename journal live in `%LOCALAPPDATA%\SignalSort`.

## Updates

Updates follow a strict pipeline: **check → review → download → verify →
stage** — and nothing is applied without your explicit confirmation
(application updates additionally require a restart). Update work never runs
while a scan or rename is active and never touches the rename journal. The
shipped build verifies package SHA-256 hashes but has **no trusted signing
key configured yet**, so no package can pass signature verification until an
operator provisions one — by design. Details: [`docs/public/UPDATE_SYSTEM.md`](docs/public/UPDATE_SYSTEM.md).

## Installation

SignalSort ships as a **self-contained ZIP for 64-bit Windows** — no
installation, and the .NET runtime is included.

1. Download and extract `SignalSort-win-x64.zip`.
2. Run `SignalSort.exe`. Windows may ask you to confirm — choose **More info →
   Run anyway** for unsigned builds.
3. Select or drag in a folder, review the preview, rename.

`TagLibSharp.dll` sits next to the executable; under its LGPL-2.1 license you
may replace it with a modified build — see
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

## Build from source

Requirements: Windows 10/11, .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`).

```powershell
git clone <repository-url>
cd SignalSort
dotnet build SignalSort.sln -c Release
dotnet test SignalSort.sln -c Release
dotnet publish src/SignalSort/SignalSort.csproj -c Release -r win-x64 --self-contained
```

Output: `src/SignalSort/bin/Release/net10.0-windows/win-x64/publish/`
(distribute as a ZIP — see `THIRD-PARTY-NOTICES.md` for why SignalSort is not
published as a single-file executable).

## Known limitations

- **Windows only** (WPF).
- Classification of audio/video requires a readable container; corrupt or
  exotic files fall back to `UnknownAudio` / `UnknownVideo`.
- Image EXIF support depends on the format (JPEG/TIFF are richest).
- Undo covers the most recent batch (older batches remain in the journal
  file but are not user-facing yet).
- No AI naming, no online lookup. Update checks are manual and opt-in;
  nothing is ever applied automatically.

## Documentation

Full documentation lives in [`docs/public/`](docs/public/):

- [`ARCHITECTURE.md`](docs/public/ARCHITECTURE.md) — how the app is structured.
- [`UPDATE_SYSTEM.md`](docs/public/UPDATE_SYSTEM.md) — update pipeline, security model, Velopack evaluation.
- [`INTEGRATIONS.md`](docs/public/INTEGRATIONS.md) — integration support (none exist yet).
- [`SECURITY.md`](docs/public/SECURITY.md) — supported versions, reporting security issues.
- [`licensing/LICENSING-DECISION.md`](docs/public/licensing/LICENSING-DECISION.md) — licensing rationale.

## Author

SignalSort is created and maintained by **Ean**
([@Eandab0t](https://github.com/Eandab0t)).

## Acknowledgements

- [TagLibSharp](https://github.com/mono/taglib-sharp) by Brian Nickel and
  contributors — media and image metadata reading (LGPL-2.1).

## License

SignalSort is © 2026 Ean, licensed under the
[Apache License 2.0](LICENSE). In short: you may use, study, modify and
redistribute it — including commercially — provided you retain the required
license and attribution notices; it grants no trademark rights in the
SignalSort name. Third-party components are covered by their own licenses —
see [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

