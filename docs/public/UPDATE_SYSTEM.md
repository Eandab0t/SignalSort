# SignalSort Update System

Status: **infrastructure + UI shipped in the beta; no update server exists yet.**
The system is complete on the client side: with an empty manifest URL (the
default) the application performs no network calls at all.

## Goals and non-goals

Goals:

- User-controlled updates: the user decides when to check, what to install,
  and when. Nothing is ever applied automatically or silently.
- Support three update kinds with one mechanism: application, content pack,
  integration (see below).
- Work fully offline. Updates are optional; every core feature works with no
  internet connection.
- Keep the rename engine completely independent of the updater. Renaming
  never waits on, trusts, or consults update state.

Non-goals (deliberate):

- No plugin platform. There is no dynamic code loading, ever.
- No dependency injection or framework adoption for its own sake.
- No auto-update scheduler in the beta.

## Update kinds

| Kind | What it changes | How it is delivered | Code shipped? |
|------|-----------------|---------------------|---------------|
| `Application` | SignalSort itself | Full package, staged, applied after user confirmation + restart | Yes - always part of a signed app release |
| `ContentPack` | Data: naming motifs, purpose hints, patterns | Declarative, schema-versioned package extracted to the staging area | **No** - data only |
| `Integration` | Connector metadata for external tools | Declarative manifest entries only | **No** - executable connector code ships **only** with app releases |

`UpdateType` (`src/SignalSort/Infrastructure/Updates/UpdateType.cs`) encodes
these three kinds.

## Security model

1. **HTTPS-only manifests.** `HttpsManifestSource` rejects any non-HTTPS
   manifest URL at construction. The Settings UI validates this again.
2. **Integrity.** Every package must match the SHA-256 hash published in the
   manifest (`ManifestValidator.VerifyPackage`).
3. **Authenticity.** Every package must carry a signature that verifies
   against the configured `ISignatureVerifier`. The shipped verifier is
   `UnverifiedSigner`, which **rejects every signature**. No trusted key and
   no default key exists in code - updates cannot be verified, and therefore
   cannot be applied, until an operator explicitly provisions a public key in
   an app release.
4. **No remote code execution.** Independently updatable packages (content
   packs, integration metadata) are declarative data validated against the
   manifest schema. They contain no executable code. Connector code, when an
   integration someday exists, is compiled into an application release and
   updated only with the application.
5. **Separation of trust.** The manifest source makes no trust decisions;
   `ManifestValidator` is the only component that decides whether a package
   is acceptable.

## Components

```
Settings (UpdateManifestUrl, HTTPS, empty by default)
        |
        v
HttpsManifestSource : IUpdateSource     -- fetch + parse manifest JSON
        |
        v
UpdateManager                           -- check / download / verify / stage
        |                                  (never applies anything itself)
        v
ManifestValidator                       -- schema + compatibility + SHA-256 + signature
        |
        v
Staging area (%TEMP%\SignalSort\updates)  -- packages land here; apply is a
                                             separate, user-confirmed step
```

- `IUpdateSource` - fetches a manifest for a channel (`stable`, `beta`...).
- `HttpsManifestSource` - HTTPS implementation; 404 means "no updates".
- `UpdateManager` - `CheckForUpdatesAsync`, `DownloadUpdateAsync`,
  `VerifyPackage`, `StagePackage`, `StageContentPack`. Staging only.
- `ManifestValidator` - manifest schema version, minimum-compatible-version
  check against the running version, package hash + signature.
- `ISignatureVerifier` / `UnverifiedSigner` - trust anchor, intentionally
  unconfigured.

## Update flow

1. User opens Settings > Updates and (optionally) enters a manifest URL.
2. User clicks **Check for updates**. The button is disabled while a scan or
   rename is in flight.
3. The manifest is fetched, validated (schema + compatibility). Available
   updates are listed with name, version and type.
4. Nothing else happens automatically. Download, verification and staging are
   explicit follow-up actions; **applying an application update always
   requires the user to confirm and restart**.

## Safety rules

- **Never update during an active rename.** Update commands are gated on
  `MainViewModel.IsBusy`, and the check method re-checks `IsBusy` on entry.
  Update work is strictly serialized with all file operations.
- **Never touch the rename journal.** The update subsystem has no reference
  to `RenameJournal` or any rename state, and staging lives under
  `%TEMP%`, never inside a scanned folder or the journal directory.
- **Rollback-safe.** Content packs stage into version-scoped directories
  (`updates\content\<id>\<version>`), so a bad pack never overwrites the
  previous one; switching back means pointing at the previous version
  directory. Application packages stage as immutable files and are applied
  only at a user-confirmed restart, so the running version is untouched
  until the moment of application.

## Manifest schema (version "1")

```json
{
  "schemaVersion": "1",
  "channel": "stable",
  "updates": [
    {
      "id": "app",
      "name": "SignalSort",
      "type": "Application",
      "version": "1.0.0",
      "minimumCompatibleVersion": "0.3.0",
      "description": "First stable release.",
      "releaseNotes": "https://example.com/notes/1.0.0",
      "downloadUrl": "https://example.com/SignalSort-1.0.0.zip",
      "sha256": "<hex sha-256 of the package file>",
      "signature": "<hex detached signature over the package bytes>",
      "dependencies": [],
      "restartRequired": true,
      "publishedAt": "2026-11-07T12:00:00Z"
    }
  ]
}
```

- `type` accepts `Application`, `ContentPack`, `Integration` (case-insensitive).
- A manifest containing **any** update whose `minimumCompatibleVersion`
  exceeds the running version is rejected wholesale — no updates from it are
  offered.
- Manifests with an unsupported `schemaVersion` are rejected wholesale.

## Enabling signature verification (operator procedure)

1. Generate a signing keypair; keep the private key offline.
2. Implement `ISignatureVerifier` with the public key (RSA via
   `RSAParameters`, or Ed25519), compile it into an application release.
3. Sign each package, publish the detached signature as hex in the manifest.
4. Until this is done, `UnverifiedSigner` guarantees no package can pass
   verification - by design.

## Evaluation: Velopack (application updates)

**Recommendation: not now. Revisit when SignalSort ships an installer-based
distribution channel.**

Velopack (velopack.net; successor to Squirrel.Windows by the same author) is
a mature .NET update framework: `vpk pack` produces release artifacts,
`VelopackApp.Build().Run()` hooks startup, and `UpdateManager` supports
`CheckForUpdates` / `DownloadUpdates` / `ApplyUpdatesAndRestart` with delta
updates and shortcut/install management.

What it would give us:

- Battle-tested application update flow with delta downloads.
- Installer + uninstaller + shortcuts handled for us.

Why it does not fit the beta today:

- It covers **application** updates only; content packs and integration
  metadata still need our declarative pipeline, so we would maintain two
  update systems.
- It changes the distribution model: Velopack wants its own installer and
  release artifacts (`.nupkg`-style packages + `RELEASES`), while the current
  distribution is a portable ZIP that must keep working (and must keep
  TagLibSharp as a separate replaceable DLL - see licensing).
- Our rules require user-confirmed application, staging separation and
  zero network by default; adopting Velopack would mean re-verifying each of
  those against its behavior instead of the ~400 lines we control.

If an installer channel is added later, the pragmatic plan is: keep the
declarative manifest system for content packs/integrations, use Velopack only
for the installed application channel, and re-verify its current API,
license and signing model against velopack.net docs at that time (this
evaluation was written from knowledge current to late 2025; no network
verification was possible while authoring).

## Migration from the standalone EXE (portable ZIP)

- **Today:** nothing to migrate. The portable ZIP remains the distribution;
  updates are opt-in via a manifest URL the user configures. A new version is
  simply a new ZIP the user extracts.
- **If Velopack (or any installer) is adopted later:** portable-ZIP users keep
  their flow; installer users get in-app updates. Existing user data is
  already outside the install directory (`%LOCALAPPDATA%\SignalSort` for
  settings, the journal next to scanned folders as designed), so an
  installer-based upgrade moves no user data.
