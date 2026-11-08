# Security Policy

## Supported versions

| Version | Supported |
|---------|-----------|
| 0.3.x (current beta) | Yes |
| older | No |

## Reporting a vulnerability

Please do not open a public issue for suspected vulnerabilities. Use
GitHub's private security advisory for this repository, or contact the
maintainer ([@Eandab0t](https://github.com/Eandab0t)) directly. Include
reproduction steps and affected versions.

## Security model

- **Offline first.** With no update manifest URL configured (the default),
  SignalSort makes no network calls at all. The only network access the app
  can perform is a user-triggered update check to an HTTPS manifest URL set
  in Settings.
- **Package verification.** Every update package must match the SHA-256 hash
  published in the manifest and carry a valid detached signature. The
  shipped build has **no trusted signing key provisioned**, so no package
  can pass signature verification until an operator installs a public key in
  an application release. This is deliberate: applying an unverified package
  is impossible, not merely discouraged.
- **No remote code execution.** Independently updatable packages are
  declarative data only. Connector code, if integrations are ever added,
  ships only inside application releases.
- **No secrets in updates.** Credentials, if a future integration needs
  them, would be stored via DPAPI/Windows Credential Manager — never in
  packages, manifests, or settings files.

Full details: [`UPDATE_SYSTEM.md`](UPDATE_SYSTEM.md).
