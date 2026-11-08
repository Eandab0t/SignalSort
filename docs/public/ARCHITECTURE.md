# SignalSort Architecture

SignalSort is a single-project WPF application (.NET, `net10.0-windows`):
it scans a folder, classifies what the files actually are, proposes names,
and renames safely with a durable journal for undo.

## Layout

```
src/SignalSort/
  Models/        AppSettings, FileClassification, enums (NamingStyle, ...)
  Core/          RenamePlanner, RenameExecutor, RenameJournal, FileScanner,
                 FileClassifier, MetadataReader, NameGenerator
  Converters/    Value converters for XAML bindings
  Infrastructure/
    Updates/     UpdateType, UpdateInfo, UpdateManifest, IUpdateSource,
                 HttpsManifestSource, ISignatureVerifier/UnverifiedSigner,
                 ManifestValidator, UpdateManager
  ViewModels/    MainViewModel (scan/classify/plan/rename/undo orchestration),
                 RelayCommand
  Views/         MainWindow code-behind; MainWindow.xaml
  Resources/     App icon, bundled license texts
tests/SignalSort.Tests/   xUnit suite (Core, Infrastructure, ViewModels)
docs/public/     ARCHITECTURE, UPDATE_SYSTEM, INTEGRATIONS, SECURITY, licensing/
```

One project, one dependency (TagLibSharp, LGPL-2.1, shipped as a separate
replaceable DLL - never single-file; see `THIRD-PARTY-NOTICES.md`).

## Main flows

**Scan → classify → plan → rename → undo**

1. `FileScanner` walks the folder (optionally recursive, hidden/system aware)
   and `FileClassifier` attaches signals per file using `MetadataReader`
   (TagLibSharp for media, content sniffing otherwise).
2. `NameGenerator` proposes names per the selected `NamingStyle`; the user
   may edit any proposed name (`NameEdited` preserves the manual value).
3. `RenamePlanner` builds a plan with collision, chain, cycle and nested-path
   detection. Case-only renames are supported via ordinal comparison with
   executor/journal exemptions. Plans run in the background with a generation
   counter so stale results never overwrite fresh UI state.
4. `RenameExecutor` applies the plan (user-confirmed) and records every
   entry in `RenameJournal`, which persists per-entry atomically and caches
   the document in memory.
5. Undo reverses the most recent completed batch via journaled original
   paths + fingerprints.

## Safety guarantees (core product promises)

- Nothing is renamed without a visible preview and explicit confirmation.
- Collisions, chains, cycles and nested moves are detected before execution;
  affected entries are skipped, never guessed.
- The journal is durable at every entry boundary; shutdown cancels work and
  waits (up to a deadline) for the operation to stop so the journal stays
  consistent.
- Settings and logs live in `%LOCALAPPDATA%\SignalSort`, never inside
  scanned folders.

## Update subsystem

Infrastructure only by default: with no manifest URL configured, the
application makes zero network calls. When the user configures an HTTPS
manifest URL, updates are check → review → download → verify → **stage**
only; applying anything requires explicit user confirmation, and application
updates additionally require a restart. Updates never run during an active
scan or rename and never touch the rename journal. Details, security model
and the Velopack evaluation: [`UPDATE_SYSTEM.md`](UPDATE_SYSTEM.md).
Integrations (none exist today): [`INTEGRATIONS.md`](INTEGRATIONS.md).

## Testing

`dotnet test` runs the xUnit suite: scanner, planner, executor, journal
(case-only + directory-aware undo), naming, manifest validation, update
manager (including a loopback HTTP server for download staging), and the
HTTPS manifest source (via stubbed `HttpMessageHandler` - no network in
tests).
