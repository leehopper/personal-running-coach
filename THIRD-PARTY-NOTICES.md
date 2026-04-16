# Third-Party Notices

This file enumerates the third-party open-source components bundled with or
consumed by RunCoach, together with their SPDX license identifiers. It is
hand-curated from the current `backend/Directory.Packages.props` and
`frontend/package.json`; the scheduled SBOM workflow added in Unit 6 of the
OSS tooling restoration will regenerate it automatically from the resolved
dependency graph once it lands.

The repository itself is licensed under `Apache-2.0` (see `LICENSE`). The
coaching prompt YAML files under `backend/src/RunCoach.Api/Prompts/` are
separately licensed under `CC-BY-NC-SA-4.0` (see the LICENSE file in that
directory). Attribution and methodology citations live in the root `NOTICE`
file.

Licenses below are identified by their SPDX short identifiers. Full license
texts are available from the upstream package registries (NuGet.org,
npmjs.com) and from https://spdx.org/licenses/.

## .NET / NuGet (backend)

Source: `backend/Directory.Packages.props`

| Package | Version | License (SPDX) |
| --- | --- | --- |
| Anthropic | 12.9.0 | MIT |
| coverlet.msbuild | 8.0.1 | MIT |
| FluentAssertions | 8.9.0 | Apache-2.0 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.5 | MIT |
| Microsoft.Extensions.AI.Evaluation | 10.4.0 | MIT |
| Microsoft.Extensions.AI.Evaluation.Quality | 10.4.0 | MIT |
| Microsoft.Extensions.AI.Evaluation.Reporting | 10.4.0 | MIT |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 10.0.5 | MIT |
| Microsoft.Extensions.Configuration.Json | 10.0.5 | MIT |
| Microsoft.Extensions.Configuration.UserSecrets | 10.0.5 | MIT |
| Microsoft.Extensions.Hosting | 10.0.5 | MIT |
| Microsoft.Extensions.TimeProvider.Testing | 10.4.0 | MIT |
| NSubstitute | 5.3.0 | BSD-3-Clause |
| SonarAnalyzer.CSharp | 10.21.0.135717 | LGPL-3.0-only |
| StyleCop.Analyzers | 1.2.0-beta.556 | MIT |
| Swashbuckle.AspNetCore | 10.1.5 | MIT |
| xunit.v3 | 3.2.2 | Apache-2.0 |
| YamlDotNet | 16.3.0 | MIT |

**Note on SonarAnalyzer.CSharp (LGPL-3.0-only):** this analyzer is a
build-time developer tool only. It is not linked into or redistributed with
the RunCoach runtime binary. The LGPL-3.0 terms apply to the analyzer
itself, not to any compiled output it inspects.

## npm (frontend)

Source: `frontend/package.json`

### Runtime dependencies

| Package | Version | License (SPDX) |
| --- | --- | --- |
| @hookform/resolvers | ^5.2.2 | MIT |
| @reduxjs/toolkit | ^2.11.2 | MIT |
| @tailwindcss/vite | ^4.2.2 | MIT |
| react | ^19.2.4 | MIT |
| react-dom | ^19.2.4 | MIT |
| react-hook-form | ^7.71.2 | MIT |
| react-redux | ^9.2.0 | MIT |
| react-router-dom | ^7.13.1 | MIT |
| tailwindcss | ^4.2.2 | MIT |
| zod | ^4.3.6 | MIT |

### Dev dependencies

| Package | Version | License (SPDX) |
| --- | --- | --- |
| @eslint/js | ^9.39.4 | MIT |
| @testing-library/jest-dom | ^6.9.1 | MIT |
| @testing-library/react | ^16.3.2 | MIT |
| @types/node | ^25.5.0 | MIT |
| @types/react | ^19.2.14 | MIT |
| @types/react-dom | ^19.2.3 | MIT |
| @vitejs/plugin-react | ^6.0.1 | MIT |
| @vitest/coverage-v8 | ^4.1.0 | MIT |
| eslint | ^9.39.4 | MIT |
| eslint-config-prettier | ^10.1.8 | MIT |
| eslint-plugin-react-hooks | ^7.0.1 | MIT |
| eslint-plugin-react-refresh | ^0.5.2 | MIT |
| eslint-plugin-sonarjs | ^4.0.2 | LGPL-3.0-only |
| globals | ^17.4.0 | MIT |
| jsdom | ^29.0.1 | MIT |
| prettier | ^3.8.1 | MIT |
| typescript | ~5.9.3 | Apache-2.0 |
| typescript-eslint | ^8.57.1 | MIT |
| vite | ^8.0.1 | MIT |
| vitest | ^4.1.0 | MIT |

**Note on eslint-plugin-sonarjs (LGPL-3.0-only):** this plugin is a
build-time developer tool only. It is not bundled into the production
frontend build.

## Root (repository tooling)

Source: `package.json`

| Package | Version | License (SPDX) |
| --- | --- | --- |
| @commitlint/cli | ^20.5.0 | MIT |
| @commitlint/config-conventional | ^20.5.0 | MIT |
| lefthook | ^2.1.4 | MIT |

## Update procedure

This file is hand-curated. When adding or removing dependencies:

1. Update `Directory.Packages.props` or the relevant `package.json`.
2. Check the upstream package metadata (nuget.org, npmjs.com) for the SPDX
   license identifier.
3. Add a row to the appropriate table above.
4. Run `dotnet restore` so the license-review workflow (Unit 6) sees the new
   resolution on the next PR.

Once the Unit 6 SBOM workflow is active, this file will be regenerated
automatically and manual edits above will be replaced by the workflow
output. Until then, keep this file in sync with the package manifests.
