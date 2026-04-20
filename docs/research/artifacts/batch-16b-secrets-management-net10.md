# Pick Postgres for keys, SOPS for secrets, user-secrets for dev

**Commit SOPS-encrypted secrets to the repo, keep `dotnet user-secrets` for the inner loop, and persist DataProtection keys in the Postgres you already run for Marten.** That combination meets OWASP ASVS v5 Level 1 today, costs effectively zero to operate, carries through from a single VPS to Render/Fly.io/Azure Container Apps without code changes, and graduates cleanly to Azure Key Vault + Managed Identity when the FTC Health Breach Notification Rule (HBNR) bar arrives at public beta. The hard decisions are fewer than the 10-tool comparison suggests: HCP Vault Secrets is being shut down July 2026, Sealed Secrets is Kubernetes-only, self-hosted Vault is single-dev overkill, Doppler still has no first-party .NET configuration provider in 2026, and the FTC amended HBNR in July 2024 to explicitly cover fitness apps that ingest from Apple Health / Strava — so a running coach is a PHR vendor under the Rule before it even ships.

The rest of this report makes that recommendation concrete: a capability matrix across ten strategies, wiring code for DataProtection-in-Postgres, a GitHub Actions snippet, per-secret rotation cadences tied to OWASP/NIST sources, and a staged migration map from MVP-0 to public beta.

## The single load-bearing decision — where DataProtection keys live

The DataProtection master key problem dominates the architecture because losing it invalidates every Identity cookie and every antiforgery token on every rebuild. Microsoft ships four in-box providers for `AddDataProtection().PersistKeysTo*`, and **for a solo dev on a .NET 10 app that already runs Postgres for Marten, `PersistKeysToDbContext<T>` is the obviously right answer** — it inherits Postgres's backup, disk encryption, connection-string bootstrap, and multi-instance consistency at zero new-infrastructure cost.

| Persistence option | NuGet (10.x) | Single-instance | Multi-instance | Encrypts keys at rest by default | Bootstrap need | Ops cost |
|---|---|---|---|---|---|---|
| File system (Docker volume) | In-framework | ✅ | ❌ (needs shared volume) | ❌ (disk FDE only) | Path + write access | 1/5 |
| **EF Core → Postgres** | **`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.1 + `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x** | ✅ | ✅ | ❌ (add `ProtectKeysWithCertificate`) | Existing Postgres conn string | **2/5** |
| Azure Blob + Key Vault | `Azure.Extensions.AspNetCore.DataProtection.Blobs` + `.Keys` + `Azure.Identity` | ✅ | ✅ | ✅ KV-wrapped | Managed Identity | 3/5 |
| StackExchangeRedis | `Microsoft.AspNetCore.DataProtection.StackExchangeRedis` 10.0.5 | ✅ | ✅ | ❌ — **Redis persistence must be on or keys vanish on restart** | Redis conn string | 3/5 |
| AWS SSM Parameter Store | `Amazon.AspNetCore.DataProtection.SSM` 4.0.2 (Mar 2026) | ✅ | ✅ | ✅ with `KMSKeyId` | IAM role | 3/5 |

Microsoft's documentation is explicit that **any explicit persistence location deregisters the default at-rest encryption**, so pair Postgres persistence with a certificate wrap (`ProtectKeysWithCertificate`) once you leave local dev; the cert can itself be a secret loaded from whichever secret store you adopt below. Neither Marten nor Wolverine ships a DataProtection integration — the `WolverineFx.Marten` package is scoped to inbox/outbox/sagas only — so sharing the database means sharing the Npgsql connection, not the schema.

```csharp
// Program.cs — DataProtection keys in Postgres, same database as Marten
public class DpKeysContext(DbContextOptions<DpKeysContext> o)
    : DbContext(o), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}

builder.Services.AddDbContext<DpKeysContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddDataProtection()
    .SetApplicationName("runcoach")
    .PersistKeysToDbContext<DpKeysContext>()
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));   // built-in rotation
    // .ProtectKeysWithCertificate(cert);            // add once leaving dev
```

Important operational note for the outbox: **Wolverine now accepts an `NpgsqlDataSource` overload for `PersistMessagesWithPostgresql`** (issue #691, closed), which is what lets you plug an `NpgsqlDataSourceBuilder.UsePeriodicPasswordProvider(...)` in so the durability agent survives Postgres password rotation without a restart. If you're still passing a raw connection string, rotation manifests as `28P01` errors until restart — migrate to the `NpgsqlDataSource` overload before your first password rotation drill.

## The ten-strategy capability matrix, compressed

Of the strategies Slice 0 listed, **HCP Vault Secrets is disqualified outright** — HashiCorp announced end-of-sale June 30 2025 and full EOL July 1 2026. **Sealed Secrets is Kubernetes-only**, so it's not a candidate until you run K8s. **Self-hosted Vault is operationally disproportionate** for one developer (unseal, Raft backups, upgrades, policy management). That leaves a real contest between SOPS, Doppler, Infisical, cloud-native, and PaaS-bundled secrets.

| Strategy | Setup cost | Runtime dep | .NET story | GH Actions | Rotation | Audit | OSS-repo fit | Solo-dev ops/month |
|---|---|---|---|---|---|---|---|---|
| Env vars + GH Secrets | 0 | None | Native `IConfiguration` | Native | Manual | GH audit log | Good | ~0h |
| **SOPS + age** | Low (CLI + 1 key) | None | CLI decrypt → env/json | `getsops/sops-install` + age key in secret | Manual, git-visible | Git log | **Ideal** (encrypted files safe to commit) | **~0.5h** |
| Doppler | Low | SaaS | **No native provider** — `doppler run` only | `DopplerHQ/cli-action@v4` (service-token, no OIDC) | Paid tiers | Paid | Good | ~1h |
| Infisical Cloud (free) | Low | SaaS | **`Infisical.Sdk` 3.0.4** + `TRENZ.Extensions.Infisical` IConfigurationProvider | `Infisical/secrets-action@v1.0.12` with OIDC | Dynamic on Pro | Yes | Good (MIT OSS) | ~1h |
| Self-host Vault CE | High | Self-host | `VaultSharp` + community config provider | `hashicorp/vault-action@v3` JWT | Dynamic secrets | Yes | Fine | 3–5h |
| HCP Vault Secrets | N/A | **Sunsetting July 2026** | — | — | — | — | — | Avoid |
| Azure Key Vault + MI | Medium | Cloud | `Azure.Extensions.AspNetCore.Configuration.Secrets` 1.5.0 + `DefaultAzureCredential` | `azure/login@v2` OIDC | Cloud-native | Full | Good | ~0.5h |
| AWS Secrets Manager | Medium | Cloud | `Amazon.Extensions.Configuration.SystemsManager` | `aws-actions/configure-aws-credentials@v4` OIDC | Cloud-native | Full | Good | ~0.5h |
| PaaS-bundled (Render/Fly/Railway/ACA) | ~0 | PaaS | Env vars / Key Vault refs (ACA) | Vendor action | Manual (PaaS) / native (ACA) | Varies | Good | ~0h |
| Sealed Secrets | Medium | K8s | K8s Secret mount | None first-party | Manual | K8s events | Ideal on K8s | K8s-only |

**SOPS + age wins on fit** for a public OSS solo-dev project because it has no runtime dependency, the encrypted file *is* a commit, the unlock key is one `AGE-SECRET-KEY-…` stored as `secrets.SOPS_AGE_KEY` in GitHub and rotated by re-encrypting, and it's CNCF Sandbox–backed (the project transferred from Mozilla to the `getsops` GitHub org in 2023; age is the recommended key type over PGP). **Infisical is the natural escalation target** if you want a web UI, audit log, and a real .NET SDK — `Infisical.Sdk` 3.0.4 shipped to NuGet in November 2025, and the community `TRENZ.Extensions.Infisical` provider gives you `builder.Configuration.AddInfisicalConfiguration()` with `__`→`:` key mapping. **Doppler remains a CLI-wrapper story** — their own docs still recommend `doppler run --name-transformer dotnet-env -- dotnet run`, so your app never knows Doppler exists. That's elegant but commits you to a SaaS dependency at runtime, which SOPS avoids.

## The bootstrap-credential decision tree

The "secret zero" problem resolves entirely by the deploy target rather than by the secret store:

- **Single VPS with Docker Compose:** use `systemd-creds encrypt` to place TPM-sealed ciphertext in `/etc/credstore.encrypted/`, reference via `LoadCredentialEncrypted=` in the systemd unit that runs `docker compose up`, and expose to containers via Compose top-level `secrets:` with `file:` pointing to `$CREDENTIALS_DIRECTORY/<name>`. This is the lowest-overhead 2026 floor for a VPS — plaintext never lives on disk, no network needed, journal-auditable.
- **Render / Fly.io / Railway:** use the platform's native secret injection; Fly encrypts `fly secrets set` at the API and re-injects per-machine at boot, Render mounts secret files at `/etc/secrets/`, Railway has Sealed Variables that become API-invisible once set. None offer external-vault reference except Azure Container Apps.
- **Azure Container Apps:** use **Key Vault references** (`keyvaultref:<URI>,identityref:<MI_ID>`) with a **user-assigned Managed Identity** — this is the Microsoft-blessed 2026 pattern and avoids the chicken-and-egg problem of the first deploy.
- **Kubernetes:** ServiceAccount token projection → Vault Kubernetes auth, optionally with the Vault Agent Injector sidecar rendering secrets to `/vault/secrets`.
- **GitHub Actions:** OIDC federation to the cloud provider or vault, never long-lived tokens. Short required-permissions block: `permissions: { id-token: write, contents: read }`.

## Recommended combination across the trajectory

**Local dev (MVP-0 today):** keep `dotnet user-secrets set` unchanged — it remains the lowest-friction option (1/5 on every axis in the comparison), stores in `%APPDATA%\Microsoft\UserSecrets\` outside the repo, and integrates via `AddUserSecrets()` automatically in Development. Add a committed `secrets.enc.yaml` (SOPS+age) only for secrets that must be shared with CI and future contributors. `.env` files with `DotNetEnv` should be avoided because they score 5/5 on commit risk and rely entirely on `.gitignore` discipline.

**CI (GitHub Actions, today):** for the eval-cache record workflow, store `ANTHROPIC_API_KEY` in a GitHub Environment called `eval` with required-reviewer protection. Use `pull_request` (not `pull_request_target`) for PR validation so fork PRs never see `secrets.*`. For integration tests that need the key, adopt the `workflow_run`-after-`pull_request` pattern — the untrusted workflow uploads artifacts, a follow-up workflow keyed on `workflow_run` runs from the default branch with secrets and consumes the artifacts. Never combine `pull_request_target` with a checkout of `github.event.pull_request.head.sha` (GitHub Security Lab's "pwn request" anti-pattern). SHA-pin every third-party action — the **tj-actions/changed-files March 2025 incident (CVE-2025-30066)** retagged every version tag `v1..v46` to a malicious commit that dumped runner memory to public logs; hash-pinned consumers were unaffected.

**MVP-1 hosted (VPS or PaaS):** SOPS-encrypted `secrets.<env>.enc.yaml` committed to the repo, decrypted by the CD workflow with an age key in `secrets.SOPS_AGE_KEY`, emitted as an environment file consumed by Docker Compose or the PaaS equivalent. DataProtection keys live in Postgres via `PersistKeysToDbContext<DpKeysContext>`. If you choose Azure Container Apps, switch secrets bootstrap to **Azure Key Vault + user-assigned Managed Identity** (Key Vault references in ACA plus `DefaultAzureCredential` in the app) — the migration is a single config block, not a rewrite. **Estimated ongoing overhead: 0.5–1 hour/month** for rotation runs plus ad hoc secret adds.

**Pre-public-beta (FTC HBNR escalation):** escalate to cloud-native KMS (Azure Key Vault or AWS Secrets Manager), wrap DataProtection keys with `ProtectKeysWithAzureKeyVault`, enable automatic rotation on the DB credentials via dynamic secrets (Vault or cloud-native), and document the full program against ASVS v5 Level 2. Infisical Cloud is the escalation option if you want to stay vendor-neutral.

### Wiring it to GitHub Actions

```yaml
# .github/workflows/deploy.yml  (SHA-pin all actions in practice)
permissions:
  id-token: write
  contents: read
jobs:
  deploy:
    environment: production      # requires reviewer approval
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: getsops/sops-install@v1
      - name: Decrypt secrets
        env:
          SOPS_AGE_KEY: ${{ secrets.SOPS_AGE_KEY }}
        run: sops -d secrets/prod.enc.yaml > .env.prod
      - name: Deploy
        run: docker compose --env-file .env.prod up -d --pull always
```

For the eval-cache record workflow, the pattern is identical but the environment is `eval` and only `ANTHROPIC_API_KEY` gets decrypted, gated by required reviewer approval so fork PRs cannot trigger it.

## Rotation cadences per named secret

Cadences derived from **OWASP Secrets Management Cheat Sheet**, **OWASP Key Management Cheat Sheet**, **NIST SP 800-57 Part 1 Rev. 5 Table §5.3.6**, and Microsoft's DataProtection defaults. ASVS v5.0.0-13.3.4 requires *scheduled* rotation "based on the organization's threat model and business requirements" — periodic is compliant at L1/L2; automated is best practice.

| Secret | Baseline cadence | Always rotate on |
|---|---|---|
| DataProtection master | **90 days** (framework default, automatic via `SetDefaultKeyLifetime`) | Key-ring exfiltration, container image leak, dev-laptop loss |
| JWT HS256 signing key | ≤ 6 months (NIST ceiling 2 years) | Suspected compromise, token-replay observed |
| Postgres role passwords (`runcoach_migrator`, `runcoach_app`) | 90 days static, or short-lived dynamic creds (Vault/IAM) preferred | Any ops departure, log-leak suspicion |
| Anthropic / third-party API keys | 90 days | Secret-scanner alert, provider forced rotation, offboarding |
| OAuth 2.0 client secrets | 6 months | Leak, IdP-forced, staff departure |
| Passkey / WebAuthn | **No scheduled rotation of user credentials**; rotate server attestation trust store quarterly with FIDO MDS updates | User-reported loss, MDS alert |
| Refresh-token encryption key | 90 days with re-wrap or dual-key decrypt window | DB dump suspected, vault CVE |

Mean-time-to-rotate on suspected compromise should be **≤ 1 hour for high-impact secrets** (prod DB password, JWT signing key, DataProtection master, API keys with PII access), ≤ 24 hours for medium-impact, ≤ 72 hours for the rest. OWASP's guidance — *"It is important that the code and processes required to rotate a key are in place before they are required"* — means the rotation runbook must be written and rehearsed in MVP-0, not when something leaks in MVP-1.

## OWASP ASVS v5 and the FTC HBNR line

**ASVS v5.0.0 was released May 2025** and reorganized secrets handling into **Chapter V13 "Configuration," section V13.3 "Secret Management"**, a distinct section renamed from v4's V6/V10/V14 content. The verbatim requirement at **v5.0.0-13.3.4** is *"Verify that key secrets have defined expiration dates and are rotated on a schedule based on the organization's threat model and business requirements."* V13.3's L1 requirements cover (a) use of a secrets-management solution rather than source code or env vars, (b) no hardcoded secrets, (c) replaceability of all keys/passwords. L2 adds access-control auditing on secret reads, documented schedules, per-environment isolation, and encryption-at-rest of the secret store itself. **Crucially, V13 does not require HSM/hardware-backed keys until Level 3** — software vaults (Key Vault standard SKU, Secrets Manager, Vault, Infisical, SOPS+age) all meet L1/L2, so the recommended combination above is compliant today.

**FTC HBNR applicability is not optional** for a running coach. The rule was amended July 29 2024 to explicitly cover fitness apps: the Federal Register text gives as an example *"a fitness app has the technical capacity to draw identifiable health information from both the user and the fitness tracker, it is a PHR, even if some users elect not to connect the fitness tracker."* Pace, heart-rate, and GPS routes are PHR identifiable health information; an app capable of ingesting from Apple Health / Strava / Garmin satisfies the "multiple sources" prong whether or not any specific user connects those sources. HBNR itself is a notification statute (60-day clock, FTC reporting at ≥500 individuals), but **FTC Section 5 creates a de facto security duty** and the 2023 enforcement trio — GoodRx ($1.5M, Feb 2023), BetterHelp ($7.8M, Mar 2023), Premom ($200K split, May 2023) — show what "reasonable security" means in practice: encryption at rest and in transit, access control, vendor DPAs, truthful privacy policies, and affirmative consent for any non-essential disclosure.

**The pre-public-release escalation point** is concretely *before the first non-alpha user ingests real biometric or route data from a third-party source*. Before any invite-only beta that connects to Apple Health, Strava, or Garmin, or any paying user, the formal program needs: a data-flow map, a breach runbook against the 60-day clock, encryption of PHR at rest and in transit, secrets management meeting at least ASVS L1 V13.3, DPAs with Anthropic and any analytics or crash reporter, and a truthful privacy policy. General availability launch raises the bar to ASVS L2.

## Migration map — "good enough now, no rewrite later"

The recommended combination is explicitly chosen so each step is **additive, not a rewrite**:

1. **MVP-0 dev → MVP-1 VPS.** Add SOPS-encrypted file + age key in GitHub secret. DataProtection keys already live in Postgres via EF — no change. Wolverine outbox already uses `NpgsqlDataSource`, so DB password rotation drops in. `Program.cs` is unchanged.
2. **MVP-1 VPS → Render / Fly.io / Railway.** Replace the `sops -d > .env.prod` step with PaaS secret set (`fly secrets import`, Render dashboard). DataProtection keys stay in Postgres. Application code unchanged.
3. **MVP-1 VPS → Azure Container Apps.** Replace SOPS decrypt with Key Vault references using user-assigned Managed Identity; optionally add `ProtectKeysWithAzureKeyVault` to wrap the Postgres-persisted DataProtection keys. Two lines of `Program.cs`; no schema change.
4. **ACA → pre-public-beta.** Migrate DataProtection to `PersistKeysToAzureBlobStorage` + `ProtectKeysWithAzureKeyVault` if you want the Microsoft-blessed production pattern; keep the EF context as fallback for a drain window. Adopt dynamic Postgres creds via Managed Identity token provider. All application code continues to work.

The things you **don't** do — and therefore don't have to undo — include standing up Vault, committing to Doppler's SaaS-only runtime dependency, picking Sealed Secrets before you run Kubernetes, and storing DataProtection keys in Redis (the "Redis doesn't persist by default" footgun is the canonical way production cookies vanish on restart).

## Conclusion — what's different after this research

The seductive answer to "how should a solo dev do production secrets?" is to pick a tool, and the Slice 0 list contained ten candidates. The honest answer is that **the bootstrap-credential layer is determined by the deploy target, the DataProtection-key layer is determined by the database you already run, and the secret-storage layer should minimize runtime dependencies for a single-dev OSS project**. That reduces the decision to: Postgres for keys, SOPS for secrets, user-secrets for dev, GitHub Environments + OIDC for CI, and cloud-native KMS only when the FTC HBNR bar makes it worth the cost.

Two non-obvious findings should change how Slice 0 is written down. First, **the EF Core DataProtection provider paired with Marten's Postgres is materially better than file-system-plus-volume** even for MVP-0 — it makes every container rebuild safe, removes the named-volume dependency, and is a literal six-line `Program.cs` change. Second, **the FTC HBNR July 2024 amendment means "public beta" is a regulatory event, not just a marketing one** — the compliance escalation needs to happen before the first user connects a fitness tracker, which is earlier than most solo-dev mental models assume. The rest of the recommendation — SOPS, user-secrets, GitHub OIDC, SHA-pinned actions, `pull_request_target` avoidance — is industry consensus baseline in 2026, and the only reason to deviate is if you've already committed to a cloud provider, in which case the answer collapses to "use that cloud's Key Vault with Managed Identity and get on with shipping."