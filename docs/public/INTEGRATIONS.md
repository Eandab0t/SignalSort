# SignalSort Integrations

**SignalSort ships zero integrations.** There are no third-party connectors,
no plugin downloads, and no integration update packages today. This document
is the contract for how integrations will work *if and when one is actually
built* - and what the update system already guarantees for them.

## Current state

- The update system ([`UPDATE_SYSTEM.md`](UPDATE_SYSTEM.md)) defines `UpdateType.Integration`
  and stages integration packages like any other declarative package.
- No integration package, manifest, or connector exists. None is faked in
  code, docs, or tests.

## Rules for any future integration

1. **Metadata updates independently; code does not.** Integration updates
   that ship while the app is installed may contain declarative data only
   (endpoint descriptors, mapping tables, schema-versioned documents).
   Executable connector code ships **only** inside an application release and
   is updated only when the application updates.
2. **Schema-versioned.** Every integration package declares a schema version;
   unsupported schemas are rejected wholesale (same rule as manifests).
3. **Rollback-safe.** Integration packages stage into version-scoped
   directories; the previously installed version directory is never mutated.
4. **Rename safety is inviolable.** An integration can read nothing from the
   rename journal and can never run during an active scan or rename.
5. **No secrets in updates.** Credentials, if an integration ever needs them,
   are stored by the user via DPAPI/Windows Credential Manager - never in
   packages, manifests, or settings JSON.

## Adding an integration (checklist for the future)

1. Define the connector in the application codebase (compiled in).
2. Define its declarative metadata schema and version it.
3. Add validation for that schema to the package pipeline.
4. Publish integration metadata through the same signed manifest flow as
   content packs.
5. Update this document with the real integration - not before.
