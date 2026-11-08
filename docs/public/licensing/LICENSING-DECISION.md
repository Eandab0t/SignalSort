# SignalSort — Licensing Decision Document

**Author:** prepared for Ean (original developer)
**Status:** **DECIDED — Apache License 2.0** (approved 2026-11-05)
**Date:** 2026-11-05

---

## 1. Your stated goals

1. Authorship clearly documented (you are the original developer).
2. Not a paid product or business.
3. People should be able to discover, learn from, and potentially use it.
4. Anyone redistributing or building on the code must retain appropriate credit.
5. No one should strip attribution and present the work as their own.
6. Concerned about losing recognition.
7. Undecided between fully open source and more restrictive terms.
8. Must not accidentally prohibit harmless personal use or community contributions.

## 2. Copyright baseline (applies before any license choice)

The moment you wrote SignalSort, copyright attached automatically — you own the
source code and the compiled program. What a license decides is **what other
people are allowed to do**. Publishing without a LICENSE file means
"all rights reserved": people may *look* at a public repo, but legally may not
reuse the code at all — which conflicts with goals 3 and 4. So a license (or an
explicit statement of terms) is needed either way.

## 3. Options compared

Full canonical texts live in [`texts/`](./texts/).

| Question | MIT | Apache-2.0 | BSD-3-Clause | PolyForm Noncommercial |
|---|---|---|---|---|
| Attribution required? | Notice must be kept in copies | Notices **and** a NOTICE file must be carried into derivative distributions | Notice must be kept in copies | `Required Notice:` line must travel with **every** copy |
| Commercial use? | Yes | Yes | Yes | **No** |
| Redistribution? | Yes | Yes | Yes | Yes (noncommercial) |
| Modifications/forks? | Yes | Yes | Yes | Yes (noncommercial) |
| Sell a modified version? | Yes | Yes | Yes | No |
| Remove your name? | From the LICENSE/notice: no (license violation). From their app's UI: yes, possible | From LICENSE/NOTICE: no (violation). App UI: yes, possible | Same as MIT | Removing the `Required Notice` line is a violation; their license ends **unless cured within 32 days of written notice** (first violation) |
| OSI-approved open source? | Yes | Yes | Yes | **No** (source-available) |
| Your control after publishing | Lowest | Moderate | Low | Highest |
| Patent grant to users | No | **Yes** (+ retaliation clause) | No | Yes (+ defense clause) |

### Plain-language summaries

**MIT** — "Do anything, just keep the notice." Maximum discoverability and
reuse; the standard for small utilities. Weakness for you: credit survives
only as a LICENSE file in their repo/distribution. Nothing stops a fork from
shipping a product where your name appears nowhere the end user will see.

**Apache-2.0** — "Do anything (including commercial), but keep the notices,
mark your changes, and carry my NOTICE file." The NOTICE mechanism is a
*standard, well-tested* way to keep "SignalSort, Copyright Ean" inside derived
distributions. Also grants users a patent license (protecting your users, and
protecting the ecosystem from patent trolls) and explicitly grants **no
trademark rights** — nobody may use the SignalSort name to promote their
derivative, which directly serves your identity concern. Slightly longer text;
occasionally overkill for small tools.

**BSD-3-Clause** — MIT plus a non-endorsement clause: derivatives may not use
your name to promote their product. A small extra identity protection;
otherwise equivalent to MIT.

**PolyForm Noncommercial 1.0.0** — "Anyone may use, change, and share it, for
anything *noncommercial* — and every copy must carry the Required Notice
line." This is the strongest fit for goals 5–6 and goal 2 (no one can build a
business on it). Two honest caveats: it is **not open source** by the OSI
definition, so some people will not touch it, it cannot be included in other
open-source projects, and workplace use counts as "commercial" even when
personal and harmless. And removing the Required Notice does not always mean
*immediate* termination — the terms give a violator a 32-day cure period
after written notice for a first offense.

> Note: a widely known "PolyForm Attribution" license does **not** exist — the
> PolyForm suite is Strict, Noncommercial, Free-Trial, Internal-Use,
> Small-Business, Perimeter and Shield (verified against the official
> polyformproject/polyform-licenses repository). The "attribution + noncommercial"
> combination is achieved here by PolyForm Noncommercial's `Required Notice`
> mechanism, which serves the same purpose.

### Why not Creative Commons (CC BY / CC BY-NC)?

Creative Commons licenses are written for creative works — text, images, music —
not source code. For software specifically:

- They never address **object code / executables**, only "the Work" — undefined
  for compiled distribution.
- CC licenses grant **no patent license**; software patents remain an unaddressed
  threat vector to users and to you.
- No software ecosystem support: GitHub's license picker treats them as
  "Other", SPDX tooling and package scanners flag them, and combining CC-NC code
  with any normal open-source dependency creates compatibility confusion.
- The CC organization itself recommends against applying CC licenses to software.

CC BY-NC 4.0 *would* technically express "credit + noncommercial" for a
standalone compiled app, but software-specific terms (PolyForm) do the same job
without those defects.

## 4. Decision (approved by Ean, 2026-11-05)

**Chosen: Apache-2.0, with a NOTICE file.**

What this means, stated plainly:

- **Apache protects attribution, not exclusivity.** Redistributors of
  derivative works must carry the applicable copyright and attribution notices
  (including the NOTICE file) — but nothing guarantees Ean's name appears on
  an application splash screen or in a fork's marketing. Anyone may take
  SignalSort, build a polished commercial version, and sell it. They must
  comply with the license; Ean does not control their business.
- Ean remains the author of his own contributions and may re-license future
  versions at any time (sole author of the published code; versions already
  shipped under Apache-2.0 remain Apache-2.0 for copies already received).

Why this fits the stated goals:

- Goals 1, 4, 5: the NOTICE file mechanism keeps `SignalSort — Copyright Ean`
  inside every derived distribution; §4(c)–(d) makes removing those notices a
  license violation, and §6 stops derivatives from using the SignalSort name
  to promote themselves.
- Goal 3: fully open source, discoverable, usable by anyone — including
  harmless personal and workplace use.
- Goal 2: permissive does not oblige Ean to support, maintain, or endorse
  commercial forks.
- Goal 7 (was undecided): re-licensing future versions remains available
  while the project stays solo-authored.

**Authorship note:** development was AI-assisted under Ean's direction. Under
U.S. Copyright Office guidance, authorship follows the human creative control
exercised over the work; this README deliberately states
"Created and maintained by Ean" — establishing developer identity without a
broader legal claim than necessary.

**Not recommended:** CC licenses for the reasons above; inventing a custom
license (legally risky and mutually unintelligible); publishing with no license
file (blocks goals 3–4 entirely).

## 5. What a license can and cannot protect

| Thing | Protected? | Notes |
|---|---|---|
| Your actual source code and the exe | **Yes** — copyright attaches automatically | A license tells others what they may do with copies. Copying without permission = infringement. |
| The *idea* of SignalSort (classify by technical signals, then rename) | **No** | Copyright covers expression, never ideas or functionality. Anyone may build their own equivalent tool from scratch, legally. A GitHub repo is evidence of priority, not ownership of the concept. |
| Algorithms/heuristics (e.g. "mono + low bitrate → VoiceMemo") | **No** as copyright; patents cover inventions | These are simple, published rules — not viable patent material (novel/non-obvious thresholds fail), and software patentability is jurisdiction-dependent. |
| Patents on something genuinely novel | Only via filing | A filed application must usually precede public disclosure: US gives a 12-month grace period after your own disclosure, but most other countries require absolute novelty — publishing first can destroy foreign rights. **Nothing in SignalSort looks like a patent candidate; if that ever changes, get professional advice before publishing.** |
| The name "SignalSort" | **Trademark territory, not copyright or license** | Unregistered ("common law") rights can arise from use in commerce once public; formal registration (e.g. USPTO) is optional and costs money — not needed to publish. The itch.io "SignalSort" already exists in an unrelated class, which would matter for any future registration. Apache-2.0 §6 explicitly grants no trademark license — derivatives can't ride your name. |
| An independently developed competitor | **No** | Clean-room reimplementation is legal. That is the price of publishing anything. |

**Not legal advice.** This document is practical guidance from tooling
knowledge, not counsel. For anything with real money or exclusivity attached,
consult an IP lawyer.

## 6. Decision checklist for Ean

- [ ] **Approve license:** Apache-2.0 (recommended) / BSD-3-Clause / MIT / PolyForm Noncommercial / none-yet
- [ ] Approve `NOTICE` file (recommended under any choice)
- [ ] Approve `THIRD-PARTY-NOTICES.md` (required by LGPL-2.1 for TagLibSharp)
- [ ] Approve `CONTRIBUTING.md` (contribution credit terms)
- [ ] Approve README "Author" section
- [ ] Decide commit-email policy: current history is authored as `Ean <ean@local>`. Fixing it to a real address (e.g. GitHub noreply) **requires rewriting history**, which was prohibited for this task. Options: publish as-is (name shows "Ean"; commits won't link to your GitHub account), or lift the restriction and run a one-time filter before the first push.
- [ ] Rename local folder to `SignalSort` (was blocked by a file lock)

## 7. Once decided (mechanics)

1. `Copy-Item docs/public/licensing/texts/<chosen>.txt LICENSE` and fill in the year/name (already filled: 2026 Ean).
2. Commit: "Add LICENSE (Apache-2.0)".
3. If PolyForm chosen instead: keep the `Required Notice: Copyright (c) 2026 Ean` line in LICENSE and README.
4. Tag the release (`v0.3.0`) when publishing.
