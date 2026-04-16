# Licensing an open-source AI running coach in 2026

**The strongest path for a solo-maintainer AI running coach is a dual-license architecture: Apache-2.0 for code, CC-BY-NC-SA-4.0 for coaching prompts, with an immediate VDOT trademark avoidance strategy.** This combination protects the core prompt IP from commercial repackaging, gives contributors a well-understood patent-granting code license, and sidesteps the dangerous gap left by the current ISC-in-package.json-with-no-LICENSE-file situation. The project also faces a concrete trademark enforcement risk: The Run SMART Project, LLC has already forced Runalyze (an established open-source running platform) to remove all VDOT features. The recommendations below address every layer of risk: license selection, trademark compliance, dependency attribution, research provenance, prompt IP, and README structure.

---

## 1. License options compared for this specific project

The table below evaluates each license against the project's actual requirements: a solo maintainer who wants coaching prompts attributed, not commercially repackaged, with an open door for community contributions.

| Criterion | MIT | Apache-2.0 | MPL-2.0 | AGPL-3.0 | BUSL-1.1 | PolyForm NC 1.0.0 |
|---|---|---|---|---|---|---|
| **Downstream use of coaching prompts** | Copy, sell, rebrand freely | Copy, sell, rebrand freely (must preserve NOTICE) | Modified prompt files must stay MPL; new files can be proprietary | Must share all source if hosted as a network service | Production use blocked until Change Date | All commercial use blocked |
| **Patent grant** | ❌ None | ✅ Explicit + retaliation clause | ✅ Explicit + retaliation | ✅ Implicit via GPLv3 §11 | ❌ None (until conversion) | ✅ For non-commercial use only |
| **GPL compatibility** | ✅ All versions | ⚠️ GPLv3 only (not v2) | ✅ GPLv2+, AGPLv3 | ✅ GPLv3 | ❌ No | ❌ No |
| **OSI-approved** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No | ❌ No |
| **SaaS loophole** | Wide open | Wide open | Partial (frontend distributed, backend not) | **Closed** — §13 requires source for network users | Closed (production blocked) | Closed (commercial blocked) |
| **Solo dev recommendation** | Poor IP protection; max adoption | Moderate — patent safety, NOTICE mechanism, but no SaaS protection | Good middle ground but HashiCorp abandoned it for BUSL | Strong protection; deters some enterprises (Google bans AGPL) | Maximum control; "not open source" label risks community | Maximum protection; minimal community |

**The ISC license currently declared in `package.json` is a critical problem.** ISC is the `npm init` default and was likely set unintentionally. More importantly, without a `LICENSE` file, there is arguably no valid license grant at all — the `package.json` field is metadata, not a binding legal instrument. Under the Berne Convention, all code defaults to "all rights reserved" without an explicit license. This must be fixed before the repo goes public.

### Recommendation: Apache-2.0 + CC-BY-NC-SA-4.0 dual license

For this project's specific goals, a **dual-license architecture** is the strongest approach:

- **Code** (`.cs`, `.tsx`, `.ts`, configs, tests): **Apache-2.0** — gives contributors an explicit patent grant, requires NOTICE file preservation (stronger attribution than MIT), is the second-most popular OSI license, and is highly enterprise-friendly. The .NET ecosystem already uses MIT/Apache-2.0 heavily, so this creates zero friction.

- **Coaching prompts** (`backend/**/Prompts/*.yaml`): **CC-BY-NC-SA-4.0** — requires attribution (BY), prohibits commercial repackaging without a separate license (NC), and forces derivatives of the prompts to use the same terms (SA). YAML prompt files are content, not code, so Creative Commons is the appropriate license family. The January 2025 U.S. Copyright Office Part 2 report confirmed that sufficiently creative human-authored prompts (detailed coaching philosophy, structured workout logic, personality definitions) are copyrightable literary works.

If the maintainer later wants to allow commercial use of prompts, they can sell a commercial license — this is the well-established **open-core business model**. If community adoption matters more than commercial protection, downgrade to **CC-BY-SA-4.0** (removes the NC restriction, is GPLv3-compatible per FSF October 2015 determination).

---

## 2. The VDOT trademark demands immediate action

**The Run SMART Project, LLC actively enforces VDOT trademark claims against open-source projects.** The most important precedent: Runalyze, a major open-source running analysis platform, was directly contacted by Dr. Jack Daniels and his team and compelled to remove all VDOT features in their v4.0 release. Runalyze's blog described the situation: *"Dr. Jack Daniels and his team at The Run SMART Project asked us to remove all vdot features as they belong to Daniels' intellectual property."* This ban covered training paces based on Daniels' suggestions and the VDOT estimation for activities.

The `tlgs/vdot` GitHub repo independently confirms *"a pattern of taking down 3rd party calculators."* Several small GitHub projects (dev-runner/RunCulator, mattncott/running-calculations, michalw1988/training-calc) still use the term "VDOT" freely, but these appear to fly under enforcement radar due to their small size. **A newly public project with visible branding will not.**

### What the law actually protects

The legal landscape has three distinct layers:

- **The word "VDOT"** is a registered trademark of The Run SMART Project, LLC (Philadelphia, PA). The official products are the VDOT Running Calculator app and the V.O2 platform at vdoto2.com. The mark covers downloadable mobile applications and online coaching services.

- **The Daniels-Gilbert equations** (VO₂ = −4.60 + 0.182258v + 0.000104v², the %VO₂max decay function, and VDOT = VO₂ / %VO₂max) are **mathematical formulas derived from empirical regression analysis** and are not copyrightable under U.S. law (*Baker v. Selden*, 1879). They are published in the 1979 *Oxygen Power* monograph and reproduced in dozens of academic papers, GitHub repos, and forum posts.

- **Zone names** (Easy, Marathon, Threshold, Interval, Repetition) are **generic exercise physiology terminology** predating Daniels' work. "Lactate threshold" dates to exercise science literature from the 1960s–70s; "interval training" to Gerschler and Reindell in the 1930s–40s. No evidence of trademark registration exists for these individual terms. However, presenting them collectively as "Daniels' five zones" or "VDOT zones" invokes the trademark.

### Recommended strategy: the Runalyze approach

Follow Runalyze's successful pattern — implement the public equations independently while avoiding VDOT branding entirely:

- Replace "VDOT" with **"effective VO₂max"** or a project-specific term like "fitness index"
- Use zone names (Easy, Threshold, Interval, Repetition, Marathon) as **generic running terms** without the "Daniels" qualifier
- **Never reproduce tables** from *Daniels' Running Formula*; compute values from the published equations
- Include the disclaimer below in README, NOTICE, and any UI "About" section

### Recommended disclaimer text

```markdown
## Trademark Notice

This application calculates estimated VO₂max and training paces using publicly
available running performance equations originally published in:

> Daniels, J. & Gilbert, J. (1979). *Oxygen Power: Performance Tables for
> Distance Runners.* Tempe, AZ.

"VDOT" and "V.O2" are registered trademarks of The Run SMART Project, LLC.
This project is **not affiliated with, endorsed by, or sponsored by**
Dr. Jack Daniels, The Run SMART Project, LLC, or the VDOT O2 platform.
For official VDOT products and coaching, visit [vdoto2.com](https://vdoto2.com).

Training zone terminology (Easy, Marathon, Threshold, Interval, Repetition)
is used as standard exercise physiology vocabulary and does not imply
endorsement or affiliation.
```

---

## 3. Third-party attribution obligations and NOTICE template

Every named dependency in this stack uses a **permissive license** — **12 of 14 are MIT**, and the remaining two (xUnit v3 and TypeScript) are Apache-2.0. No copyleft dependencies exist, so there are no viral licensing concerns. Apache-2.0 dependencies carry slightly stricter requirements: the NOTICE file must be preserved, changes must be stated, and an explicit patent grant applies.

| Dependency | License (SPDX) | Copyright Holder |
|---|---|---|
| ASP.NET Core | `MIT` | .NET Foundation and Contributors |
| EF Core | `MIT` | .NET Foundation and Contributors |
| Marten | `MIT` | Jeremy D. Miller et al. / JasperFx Software |
| Wolverine | `MIT` | JasperFx Software |
| Microsoft.Extensions.AI.Evaluation | `MIT` | Microsoft Corporation |
| Anthropic SDK for .NET | `MIT` | Anthropic, PBC |
| xUnit v3 | `Apache-2.0` | .NET Foundation and Contributors |
| React 19 | `MIT` | Meta Platforms, Inc. and affiliates |
| Redux Toolkit | `MIT` | Mark Erikson |
| Tailwind CSS v4 | `MIT` | Tailwind Labs, Inc. |
| shadcn/ui | `MIT` | shadcn (Shahid Shaikh) |
| Vite 8 | `MIT` | VoidZero Inc. and Vite contributors |
| Zod v4 | `MIT` | Colin McDonnell |
| TypeScript | `Apache-2.0` | Microsoft Corporation |

Note on **shadcn/ui**: the project's FAQ previously stated "No attribution required," which conflicted with the MIT license. That line was removed (PR #6605). The MIT license in their LICENSE.md is authoritative. Since shadcn/ui is a code-copy distribution model (not an npm install), copied component files should retain the MIT reference.

For automated scanning, use **`DotnetThirdPartyNotices`** or **`nuget-license`** for the .NET backend and **`license-checker --json`** for the npm frontend. Integrate **`vite-plugin-license`** to auto-generate a `THIRD_PARTY_LICENSES.txt` during Vite builds.

### THIRD-PARTY-NOTICES.md template

```markdown
# Third-Party Notices

This file lists third-party software, scientific works, and trademarks
referenced by this project.

Generated: 2026-04-15
SPDX document: see `reuse spdx` output

---

## Scientific Works & Formulas

### Daniels-Gilbert Running Performance Equations

> Daniels, J. & Gilbert, J. (1979). *Oxygen Power: Performance Tables
> for Distance Runners.* Tempe, AZ.

Mathematical formulas for estimating VO₂ from velocity and sustainable
%VO₂max from duration. Formulas are not copyrightable (Baker v. Selden,
1879). Implementation is original. "VDOT" is a registered trademark of
The Run SMART Project, LLC and is not used in this software.

### Tanaka Age-Predicted Maximum Heart Rate Formula

> Tanaka, H., Monahan, K.D., & Seals, D.R. (2001). Age-predicted
> maximal heart rate revisited. *Journal of the American College of
> Cardiology*, 37(1), 153–156.
> https://doi.org/10.1016/S0735-1097(00)01054-8

Formula: HRmax = 208 − 0.7 × age. Mathematical formula; not
copyrightable. Implementation is original.

---

## Backend Dependencies (.NET)

### ASP.NET Core / Entity Framework Core
- License: MIT
- Copyright: © .NET Foundation and Contributors
- https://github.com/dotnet/aspnetcore

### Marten (Document DB / Event Sourcing)
- License: MIT
- Copyright: © Jeremy D. Miller, Babu Annamalai, Oskar Dudycz,
  Joona-Pekka Kokko — JasperFx Software
- https://github.com/JasperFx/marten

### Wolverine (Messaging / Background Processing)
- License: MIT
- Copyright: © JasperFx Software
- https://github.com/JasperFx/wolverine

### Microsoft.Extensions.AI.Evaluation
- License: MIT
- Copyright: © Microsoft Corporation
- https://nuget.org/packages/Microsoft.Extensions.AI.Evaluation

### Anthropic SDK for .NET
- License: MIT
- Copyright: © Anthropic, PBC
- https://nuget.org/packages/Anthropic

### xUnit v3
- License: Apache-2.0
- Copyright: © .NET Foundation and Contributors
- https://github.com/xunit/xunit
- Note: Apache-2.0 requires NOTICE preservation and grants an explicit
  patent license.

---

## Frontend Dependencies (npm)

### React 19
- License: MIT
- Copyright: © Meta Platforms, Inc. and affiliates
- https://github.com/facebook/react

### Redux Toolkit
- License: MIT
- Copyright: © 2018 Mark Erikson
- https://github.com/reduxjs/redux-toolkit

### Tailwind CSS v4
- License: MIT
- Copyright: © Tailwind Labs, Inc.
- https://github.com/tailwindlabs/tailwindcss

### shadcn/ui
- License: MIT
- Copyright: © shadcn (Shahid Shaikh)
- https://github.com/shadcn-ui/ui
- Note: Code-copy distribution model. Built on Radix UI (MIT).

### Vite 8
- License: MIT
- Copyright: © 2019-present VoidZero Inc. and Vite contributors
- https://github.com/vitejs/vite

### Zod v4
- License: MIT
- Copyright: © Colin McDonnell
- https://github.com/colinhacks/zod

### TypeScript
- License: Apache-2.0
- Copyright: © Microsoft Corporation
- https://github.com/microsoft/TypeScript

---

## Transitive Dependencies

A full machine-readable list of transitive dependency licenses is
generated during CI. See:
- Backend: `artifacts/backend-licenses.json` (via `nuget-license`)
- Frontend: `artifacts/frontend-licenses.json` (via `license-checker`)
```

---

## 4. Research artifact provenance: per-batch PROVENANCE.md wins

Three approaches exist for documenting the provenance of AI-generated paraphrased research in `docs/research/artifacts/`: YAML front-matter per file, a directory-level README, or a central PROVENANCE.md. **The per-batch directory approach is the right granularity** because the research artifacts share provenance within each batch (same AI tool, same session, same date) but differ across batches.

The REUSE 3.2 specification (published 2024-07-03) requires every covered file to have licensing information but has no specific guidance for AI-generated content provenance. Provenance is a separate metadata layer that rides alongside REUSE compliance. The recommended approach uses **REUSE.toml** for licensing (bulk-annotating `docs/research/artifacts/**` as CC-BY-SA-4.0) and **per-batch PROVENANCE.md** files for AI tool attribution, editorial level, and source disposition.

APA style (updated September 2025) requires citing AI tools as: *Company. (Year). Title [Generative AI chat]. Model Name. URL.* The IEEE requires disclosure in an acknowledgments section. Both conventions support the per-batch approach since each batch was a distinct AI research session.

### Provenance template (per-batch file)

Place this file as `docs/research/artifacts/<batch-name>/PROVENANCE.md`:

```markdown
---
provenance:
  ai_tool: "Claude Sonnet 4 (Anthropic)"
  ai_tool_version: "claude-sonnet-4-20250514"
  generation_date: "2026-03-15"
  human_editor: "Your Name <email@example.com>"
  editorial_level: "substantially_paraphrased"
  research_query: "Lactate threshold training adaptations in recreational runners"
  source_disposition: >
    AI-generated summaries of web-accessible research. Original sources
    were not independently verified against primary publications.
    Content has been paraphrased and reorganized by the human editor.
  disclaimer: >
    These documents are research notes, not authoritative citations.
    Verify claims against primary sources before relying on them.
  files:
    - lactate-threshold-mechanisms.md
    - threshold-training-protocols.md
    - recreational-vs-elite-adaptations.md
---

# Batch: Lactate threshold training research

| File | Topic | Editorial level |
|------|-------|-----------------|
| lactate-threshold-mechanisms.md | Physiological mechanisms | Substantially paraphrased |
| threshold-training-protocols.md | Training protocol survey | Substantially paraphrased |
| recreational-vs-elite-adaptations.md | Population differences | Lightly edited |
```

### REUSE.toml entry for research artifacts

```toml
[[annotations]]
path = "docs/research/artifacts/**"
precedence = "override"
SPDX-FileCopyrightText = "2026 Your Name <email@example.com>"
SPDX-License-Identifier = "CC-BY-SA-4.0"
SPDX-FileComment = "AI-generated research summaries. See PROVENANCE.md in each batch directory."
```

---

## 5. Coaching prompt IP: dual-licensing is the defensible play

The coaching prompts in `backend/**/Prompts/*.yaml` are the project's core differentiator. Three concrete 2025–2026 precedents show how AI product repositories handle this:

**f/prompts.chat** (160k+ GitHub stars, formerly "Awesome ChatGPT Prompts") uses explicit dual licensing: MIT for the web app code, **CC0 for prompt content**. This is the highest-profile example of separating code from prompts in a single repo. It demonstrates the pattern works mechanically, though CC0 (public domain dedication) is the opposite of the protection this project needs.

**thehimel/cursor-rules-and-prompts** takes the opposite extreme: a fully proprietary license specifically to protect Cursor rules/prompts as IP, forbidding sharing, modification, and commercial use entirely. This shows the instinct to protect prompt content even when code patterns are standard, though it eliminates community participation.

**felixrieseberg/claude-coach** is directly relevant — an open-source AI endurance coaching tool (triathlon, marathon, ultra) that works as a Claude skill. It uses **MIT for everything**, including coaching prompts. This represents the "just ship it" approach where adoption matters more than IP protection.

The January 2025 U.S. Copyright Office Part 2 report on AI copyrightability confirmed that **human-authored prompts with sufficient creative expression are copyrightable literary works**. Detailed coaching prompts — containing philosophy, periodization logic, personality definitions, and domain-specific training wisdom — clearly meet this bar. Simple functional prompts ("You are a running coach") do not. The YAML files in this project, which encode structured coaching methodology, sit firmly in copyrightable territory.

**Recommended dual-license structure:**

```
backend/**/Prompts/*.yaml  →  CC-BY-NC-SA-4.0
everything else             →  Apache-2.0
```

This is expressed in REUSE.toml as:

```toml
[[annotations]]
path = "backend/**/Prompts/*.yaml"
precedence = "override"
SPDX-FileCopyrightText = "2026 Your Name <email@example.com>"
SPDX-License-Identifier = "CC-BY-NC-SA-4.0"

[[annotations]]
path = ["backend/**/*.cs", "frontend/**/*.ts", "frontend/**/*.tsx"]
SPDX-FileCopyrightText = "2026 Your Name <email@example.com>"
SPDX-License-Identifier = "Apache-2.0"
```

The `LICENSES/` directory must contain both `Apache-2.0.txt` and `CC-BY-NC-SA-4.0.txt` (downloadable via `reuse download Apache-2.0 CC-BY-NC-SA-4.0`).

---

## 6. README sections ready to paste

The following sections are designed for a newly-public repo following 2026 OSS conventions with REUSE 3.2 compliance. Drop them into the top of your README.md:

```markdown
<!-- SPDX-FileCopyrightText: 2026 Your Name <email@example.com> -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Project Name — AI Running Coach

[![License: Apache-2.0](https://img.shields.io/badge/Code-Apache--2.0-blue.svg)](LICENSE)
[![License: CC BY-NC-SA 4.0](https://img.shields.io/badge/Prompts-CC--BY--NC--SA--4.0-lightgrey.svg)](CONTENT_LICENSE)
[![REUSE compliant](https://api.reuse.software/badge/github.com/yourname/project)](https://api.reuse.software/info/github.com/yourname/project)

> An open-source AI-powered running coach built with .NET 10 and React 19.

---

## License

This project uses a **dual-license structure**:

| Scope | License | SPDX |
|-------|---------|------|
| Source code, tests, configuration | [Apache License 2.0](LICENSE) | `Apache-2.0` |
| Coaching prompts (`backend/**/Prompts/*.yaml`) | [CC BY-NC-SA 4.0](CONTENT_LICENSE) | `CC-BY-NC-SA-4.0` |
| Research artifacts (`docs/research/artifacts/`) | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/) | `CC-BY-SA-4.0` |

**Source code** is free to use, modify, and distribute under Apache-2.0
terms, including commercial use.

**Coaching prompt content** requires attribution, prohibits commercial
repackaging without a separate license, and requires derivatives to use
the same terms. For commercial licensing of prompt content,
contact [email@example.com](mailto:email@example.com).

See [NOTICE](NOTICE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
for attribution details. This project aims for
[REUSE 3.2](https://reuse.software/spec-3.2/) compliance.

## Trademark notice

This application calculates estimated VO₂max and training paces using
publicly available running performance equations originally published in:

> Daniels, J. & Gilbert, J. (1979). *Oxygen Power: Performance Tables
> for Distance Runners.* Tempe, AZ.

**"VDOT"** and **"V.O2"** are registered trademarks of The Run SMART
Project, LLC. This project is **not affiliated with, endorsed by, or
sponsored by** Dr. Jack Daniels, The Run SMART Project, LLC, or the
VDOT O2 platform. For official VDOT products, visit
[vdoto2.com](https://vdoto2.com).

Training zone terminology (Easy, Marathon, Threshold, Interval,
Repetition) is used as standard exercise physiology vocabulary.

## Attribution

This project uses the following scientific formulas:

- **Daniels-Gilbert equations** — Daniels, J. & Gilbert, J. (1979).
  *Oxygen Power.* Mathematical performance estimation formulas.
- **Tanaka formula** — Tanaka, H., Monahan, K.D., & Seals, D.R. (2001).
  Age-predicted maximal heart rate revisited. *JACC*, 37(1), 153–156.

Full dependency attribution: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)

## Contributing

Contributions are welcome under the project's license terms.
See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

By submitting a pull request, you agree that your code contributions
are licensed under Apache-2.0 and any prompt contributions under
CC-BY-NC-SA-4.0, consistent with the project's dual-license structure.
```

---

## 7. Out-of-scope items flagged for follow-up

The following are important for a healthy open-source project but are separate workstreams from this licensing audit:

- **CONTRIBUTING.md** — Define contribution workflow, branch naming, PR template, CLA or DCO requirement (a DCO sign-off via `git commit -s` is lighter than a full CLA and sufficient for this project size)
- **CODE_OF_CONDUCT.md** — Adopt Contributor Covenant 2.1 (the 2026 standard) or a project-specific code of conduct
- **"Good first issue" tagging** — Label strategy for onboarding contributors; consider `good-first-issue`, `help-wanted`, and `prompt-contribution` labels
- **CI license scanning** — Add `reuse lint` to GitHub Actions via `fsfe/reuse-action@v4`; add `license-checker` to npm scripts for policy enforcement
- **CLA vs. DCO decision** — If dual-licensing with a commercial option, a CLA (Contributor License Agreement) gives the maintainer flexibility to relicense contributions; a DCO (Developer Certificate of Origin) is simpler but doesn't grant relicensing rights

---

## Files to create before going public

To summarize the concrete deliverables, these files should be created and committed before the repository is made public:

| File | Content | Source |
|------|---------|--------|
| `LICENSE` | Full Apache-2.0 text | `reuse download Apache-2.0` or [apache.org/licenses/LICENSE-2.0.txt](https://www.apache.org/licenses/LICENSE-2.0.txt) |
| `CONTENT_LICENSE` | Full CC-BY-NC-SA-4.0 text | `reuse download CC-BY-NC-SA-4.0` |
| `LICENSES/Apache-2.0.txt` | REUSE-required license file | `reuse download Apache-2.0` |
| `LICENSES/CC-BY-NC-SA-4.0.txt` | REUSE-required license file | `reuse download CC-BY-NC-SA-4.0` |
| `LICENSES/CC-BY-SA-4.0.txt` | For research artifacts | `reuse download CC-BY-SA-4.0` |
| `NOTICE` | Short attribution (required by Apache-2.0) | Include project name, copyright, and trademark disclaimer |
| `THIRD-PARTY-NOTICES.md` | Full dependency + scientific attribution | Template provided in §3 above |
| `REUSE.toml` | Bulk SPDX annotations for all file categories | Template provided in §4–5 above |
| `package.json` | Change `"license": "ISC"` → `"license": "Apache-2.0"` | Manual edit |
| `README.md` | Add license, trademark, attribution sections | Template provided in §6 above |

The `NOTICE` file (required by Apache-2.0 §4(d)) should be minimal:

```
Project Name — AI Running Coach
Copyright 2026 Your Name

Licensed under the Apache License, Version 2.0.
Coaching prompt content licensed under CC-BY-NC-SA-4.0.

This product includes publicly available running performance equations
from Daniels, J. & Gilbert, J. (1979), Oxygen Power, and Tanaka, H.
et al. (2001), JACC 37(1):153-156.

"VDOT" is a registered trademark of The Run SMART Project, LLC.
This project is not affiliated with or endorsed by The Run SMART Project.
```

---

## Conclusion

Three actions carry the highest urgency. First, **add a LICENSE file immediately** — the current state (ISC in package.json, no LICENSE file) means downstream users have no valid license grant and the maintainer has no legal protection framework. Second, **rename any "VDOT" references in code and UI** to "effective VO₂max" or a custom term before going public — the Runalyze enforcement precedent is concrete and recent. Third, **implement the dual-license REUSE.toml** so that every file in the repo has unambiguous licensing metadata from day one.

The broader 2025–2026 licensing trend validates this approach. Redis, Elastic, and Grafana all moved to AGPL-3.0 to protect against cloud competitors, and the "open-core" dual-license pattern (open code + restricted content) is now the standard for AI-product repositories. For a solo maintainer, Apache-2.0 + CC-BY-NC-SA-4.0 threads the needle: it attracts contributors who understand Apache-2.0, protects the coaching prompts that constitute the product's actual differentiation, and creates a clean commercial licensing path if the project grows.