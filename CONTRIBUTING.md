# Contributing to SignalSort

Thank you for considering a contribution. SignalSort is a solo-authored
project maintained by **Ean**, and these terms exist so that your work gets
credited properly and the project's licensing stays simple.

## How your contribution is credited

- **You keep the copyright to your own contribution.** Submitting a change
  does not transfer ownership to the project author.
- Contributions are acknowledged in the commit history under your name and,
  for notable contributions, in the README's *Acknowledgements* section.
- By submitting a pull request, you agree that your contribution is licensed
  **under the project's license** (see `LICENSE`), so it can be distributed
  with SignalSort. You are not asked to sign anything; your pull request is
  your agreement.
- Do not remove or alter existing copyright or attribution notices, including
  the project author's and third-party notices in `THIRD-PARTY-NOTICES.md`.

## Ground rules

- One topic per pull request; keep changes focused.
- Tests live in `tests/SignalSort.Tests` — new behavior should come with
  tests, and `dotnet test SignalSort.sln` must pass.
- Match the existing code style (`.editorconfig`); no reformatting drives.
- Temporary files and test data belong in `Path.GetTempPath()`-scoped
  directories, never in user folders.

## Reporting bugs

Open an issue with: Windows version, SignalSort version, the folder structure
that triggered the behavior (names redacted if you prefer), and what you
expected vs. what happened. Do not paste rename journals — they contain your
local file paths.
