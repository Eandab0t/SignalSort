# SignalSort

**Identify. Understand. Rename.**

SignalSort analyzes technical characteristics and embedded metadata to identify
what files actually are, then generates meaningful filenames and lets you
rename them safely.

> It understands your files, not just their extensions.

A rename tool can insert dates and counters — but only after *someone* decides
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

SignalSort works entirely offline. No file contents, names or telemetry ever
leave your machine. No account, no network access, no cloud. Settings and the
rename journal live in `%LOCALAPPDATA%\SignalSort`.

## Installation

No installer: SignalSort ships as a single self-contained `.exe`
(~70 MB, .NET runtime included).

1. Download `SignalSort.exe`.
2. Double-click. Windows may ask you to confirm — choose **More info → Run
   anyway** for unsigned builds.
3. Select or drag in a folder, review the preview, rename.

## Build from source

Requirements: Windows 10/11, .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`).

```powershell
git clone <repository-url>
cd SignalSort
dotnet build SignalSort.sln -c Release
dotnet test SignalSort.sln -c Release
dotnet publish src/SignalSort/SignalSort.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output: `src/SignalSort/bin/Release/net10.0-windows/win-x64/publish/SignalSort.exe`

## Known limitations

- **Windows only** (WPF).
- Classification of audio/video requires a readable container; corrupt or
  exotic files fall back to `UnknownAudio` / `UnknownVideo`.
- Image EXIF support depends on the format (JPEG/TIFF are richest).
- Undo covers the most recent batch (older batches remain in the journal
  file but are not user-facing yet).
- No AI naming, no online lookup, no automatic updates — deliberate scope.

## Licensing

No license has been selected yet. Licensing is an explicit decision deferred
until first publication.
