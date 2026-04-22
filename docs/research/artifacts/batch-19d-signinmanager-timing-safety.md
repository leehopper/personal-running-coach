# SignInManager timing-safety in .NET 10 — not built-in

**Verdict: NO.** `SignInManager<TUser>.PasswordSignInAsync(userName, password, …)` in ASP.NET Core Identity 10 (April 2026 `main`) does **not** perform a dummy password-hash check when the user is not found. The framework returns `SignInResult.Failed` in microseconds on the unknown-user branch while a known user triggers a **PBKDF2-HMAC-SHA512 verification at 100 000 iterations** (tens of milliseconds). This asymmetry is remotely observable and is a textbook user-enumeration side-channel. RunCoach's `AuthController.Login` **must implement the manual mitigation** that Slice 0 §Unit 2 permits as its fallback. The "or" in the spec is not a choice — in .NET 10 it is a requirement.

The rest of this artifact documents the primary-source evidence, the correct mitigation code, the surrounding enumeration vectors worth closing at the same time, and test-assertion guidance for T02.5.

## What the .NET 10 source code actually does

The canonical file is `src/Identity/Core/src/SignInManager.cs` on `dotnet/aspnetcore` `main` (no `release/10.0` divergence relevant here; the Identity code has been stable across .NET 8, 9, and 10). The string overload of `PasswordSignInAsync` looks like this:

```csharp
public virtual async Task<SignInResult> PasswordSignInAsync(string userName, string password,
    bool isPersistent, bool lockoutOnFailure)
{
    var startTimestamp = Stopwatch.GetTimestamp();
    var user = await UserManager.FindByNameAsync(userName);
    if (user == null)
    {
        _metrics?.AuthenticateSignIn(typeof(TUser).FullName!, AuthenticationScheme,
            SignInResult.Failed, SignInType.Password, isPersistent, startTimestamp);
        return SignInResult.Failed;
    }
    return await PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure);
}
```

**The `user == null` branch returns immediately.** The only work performed on that path is the `FindByNameAsync` round-trip (a single indexed SELECT on `AspNetUsers` by `NormalizedUserName`) plus a metrics hook that is a no-op when no `IMeterFactory` is registered. There is no call to `UserManager.CheckPasswordAsync`, no `PasswordHasher.VerifyHashedPassword`, no `HashPassword`, no `TimingAttackGuard` helper — none of those constructs exist in the Identity codebase. Grep for `"timing"`, `"constant"`, `"enumeration"`, `"dummy"`, or `"NoOpHash"` in `src/Identity/` returns zero relevant hits.

The user-found branch delegates to `PasswordSignInAsync(TUser, …)` → `CheckPasswordSignInAsync(user, password, lockoutOnFailure)` → private `CheckPasswordSignInCoreAsync` → `UserManager.CheckPasswordAsync(user, password)` → `PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password)`. That final call executes **PBKDF2-HMAC-SHA512 with 100 000 iterations** — the defaults declared in `src/Identity/Extensions.Core/src/PasswordHasherOptions.cs`:

```csharp
public PasswordHasherCompatibilityMode CompatibilityMode { get; set; }
    = PasswordHasherCompatibilityMode.IdentityV3;
public int IterationCount { get; set; } = 100_000;
```

The V3 format header in `PasswordHasher.cs` confirms the algorithm: `PBKDF2 with HMAC-SHA512, 128-bit salt, 256-bit subkey, 100000 iterations`. These values have been unchanged since .NET 7 (bumped from 10 000 HMAC-SHA256 in .NET 6). On commodity server hardware a single verification costs roughly **40–80 ms**, which is the exact delta an enumerator measures.

**No PR has merged to close this gap.** The closest tracked work is issue #54542 ("Prevent Identity lockout mechanism telling hackers which UserNames exist", opened March 2024, labeled `design-proposal`, assigned to `blowdart`, closed without a shipped fix). #54542 targets a *different* leak — after N failed attempts, `SignInResult.LockedOut` appears only for real users — but it confirms the maintainers are aware of the enumeration-class problem and have chosen not to patch the library. The reply trail treats enumeration-hardening as application responsibility, consistent with Microsoft's pattern of shipping primitives rather than policy. As of April 2026, **no CVE, no security advisory, and no merged fix** exists for the login-path timing delta.

A practical corollary: the default scaffolded Razor templates (`Areas/Identity/Pages/Account/Login.cshtml.cs` in the `Microsoft.AspNetCore.Identity.UI` package) call `_signInManager.PasswordSignInAsync(Input.Email, …)` with no pre-lookup and **inherit the vulnerability**. Teams that believe "we're using the official template so we're safe" are mistaken.

## What authoritative guidance says in 2026

Microsoft's official documentation does **not** claim `PasswordSignInAsync` is timing-safe — and does not discuss timing-safety for the login endpoint at all. The ASP.NET Core Identity docs on `learn.microsoft.com` cover lockout, two-factor, account confirmation, and generic error messages, but the word "enumeration" and the phrase "timing attack" do not appear on the relevant pages. Microsoft's stance is effectively "we give you `IPasswordHasher<TUser>` with a documented time-consistent `VerifyHashedPassword`; use it." The `IPasswordHasher` XML doc on `VerifyHashedPassword` explicitly states *"Implementations of this method should be time consistent"* — but that protects against comparing two hashes, not against distinguishing "ran the hasher" from "didn't run the hasher."

**OWASP is unambiguous.** The Authentication Cheat Sheet requires that login "go through the same process no matter what the user or password is, so that … the application does not reveal any information about the existence of the account." The Forgot-Password Cheat Sheet adds "responses return in a consistent amount of time to prevent an attacker enumerating which accounts exist … achieved by using asynchronous calls or by making sure that the same logic is followed, instead of using a quick exit method." ASVS v4 §2.2.3 and v5 §6.2.3 require protection against user enumeration; the WSTG account-enumeration chapter lists timing deltas as a first-class discovery vector alongside error-message differences. OWASP Top 10 2021 A07 (Identification & Authentication Failures) lists unhardened enumeration as a defining failure mode.

**Community technical consensus** (Andrew Lock's PasswordHasher deep-dive; Rock Solid Knowledge / Duende on IdentityServer custom UIs; Cyberis, TrustedSec, and Antradar's empirical demonstrations against ASP.NET Core) converges on the same mitigation: never short-circuit when the user is null — always execute an equivalent PBKDF2 pass. Cofoundry (a .NET CMS) goes further and wraps every authentication attempt in a "minimum random duration" envelope, which you should treat as the belt-and-braces option, not a substitute for the dummy hash.

## The correct dummy-hash call

Two candidates, both at roughly identical cost because PBKDF2 dominates:

- **`HashPassword(default!, password)`** — generates a salt, runs PBKDF2 once, formats the result. One PBKDF2 of 100 000 HMAC-SHA512 iterations. Throws away the output.
- **`VerifyHashedPassword(default!, KnownFakeHash, password)`** — parses the V3 hash header, runs PBKDF2 once on the provided password, does a constant-time compare. Also one PBKDF2 of 100 000 HMAC-SHA512 iterations.

**Use `VerifyHashedPassword` with a cached fake hash.** It traverses the *same code path* as a real-user failure (parse header → derive key → constant-time compare → return `Failed`), which gives the tightest timing parity. `HashPassword` additionally calls `RandomNumberGenerator.Fill` for the salt; that is sub-microsecond and effectively negligible next to PBKDF2, but `VerifyHashedPassword` avoids it and is the version OWASP, Andrew Lock, and Cyberis all recommend. Cache one fake hash once at startup (hash a throwaway password at process init) rather than recomputing — otherwise you pay an extra 40–80 ms on the unknown-user branch *per request* and make the unknown-user path **slower** than the known-user path, flipping the leak rather than closing it.

One correction to the spec text: **there is no `HashPasswordAsync` method on `IPasswordHasher<TUser>`.** The interface exposes only synchronous `HashPassword(TUser user, string password)` and `VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)`. The spec's snippet `await userManager.PasswordHasher.HashPasswordAsync(default, password)` will not compile. The production call is synchronous and should not be `await`ed.

## Recommended AuthController.Login for T02.4

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace RunCoach.Api.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    SignInManager<AppUser> signInManager,
    UserManager<AppUser> userManager,
    IPasswordHasher<AppUser> passwordHasher,
    ILogger<AuthController> logger) : ControllerBase
{
    // Pre-computed at first use; V3 PBKDF2-HMAC-SHA512 / 100k iterations.
    // Value is irrelevant — only its parse-ability and cost parity matter.
    private static readonly string DummyHash =
        new PasswordHasher<AppUser>().HashPassword(new AppUser(), "__runcoach_dummy__");

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        // ModelState binding errors are handled by [ApiController] and return 400 before
        // reaching here; treat every non-400 path as a candidate for timing parity.

        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            // Burn one PBKDF2 pass on the supplied password to match the timing of
            // SignInManager's real verification branch. Result is discarded.
            _ = passwordHasher.VerifyHashedPassword(new AppUser(), DummyHash, request.Password);
            logger.LogInformation("Login failed: unknown email (timing-masked)");
            return Unauthorized(GenericAuthFailure());
        }

        // Slice 0: lockout disabled; revisit in MVP-1 (see audit note below).
        var result = await signInManager.PasswordSignInAsync(
            user, request.Password, isPersistent: true, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            return Ok(); // cookie already written by SignInManager
        }

        // IsLockedOut / IsNotAllowed / RequiresTwoFactor collapse to the same generic 401
        // to avoid leaking account state. Log the distinction server-side only.
        logger.LogInformation("Login failed for {UserId}: {Result}", user.Id, result);
        return Unauthorized(GenericAuthFailure());
    }

    private static ProblemDetails GenericAuthFailure() => new()
    {
        Type   = "https://runcoach.app/problems/invalid-credentials",
        Title  = "Invalid credentials",
        Status = StatusCodes.Status401Unauthorized,
        Detail = "Email or password is incorrect."
    };
}

public sealed record LoginRequest(string Email, string Password);
```

Four design points in that code deserve explicit justification. First, `DummyHash` is computed once via a `static readonly` initializer; the cost is paid at module load, not per request. Second, the discarded `_ =` of the verify result is intentional — a compiler that optimizes away the call would break the mitigation, but PBKDF2 is an extern P/Invoke to `Rfc2898DeriveBytes`, which the JIT cannot elide. Third, the `RequiresTwoFactor` / `IsLockedOut` / `IsNotAllowed` branches are collapsed into the same generic 401 body; returning distinct codes for these would re-open the enumeration channel on a different axis. Fourth, `isPersistent: true` matches the Slice 0 spec but applies only on success — it does not affect the failure path's timing.

## Security audit note — closing adjacent leaks

**Lockout (MVP-1).** When `lockoutOnFailure: true` lands, the known-user failure branch gains a DB write (`UserManager.AccessFailedAsync`) that the unknown-user branch does not. That reopens the gap. Mitigations, in order of preference: (a) override `SignInManager` and add a tracked "failed attempt" counter keyed by submitted-email-normalized-hash for unknown users so both branches write; (b) route every failed login through a fixed-latency delay (e.g. `Task.Delay(Random.Shared.Next(80, 140), ct)` after the 401 is formed but before flushing); (c) accept the residual leak and rely on IP-based rate limiting (the `Microsoft.AspNetCore.RateLimiting` fixed-window policy) to bound enumeration throughput. Option (b) is the simplest and aligns with OWASP's "make responses uniform" recommendation. Revisit when MVP-1 ticket lands.

**Registration endpoint.** `userManager.CreateAsync` will return `DuplicateUserName` / `DuplicateEmail` IdentityError codes. Exposing those in the 400 response is equivalent to a public enumeration API. Map any `Duplicate*` error to the same 201-with-email-sent response as a successful registration (the confirmation email itself tells the real owner; the enumerator learns nothing). This pattern is in OWASP's registration-hardening guidance and is trivial to add.

**Password-reset endpoint.** `POST /forgot-password` must return the same 200 whether the email exists or not. Always execute the token-generation work (or a dummy equivalent) to equalize timing; `UserManager.GeneratePasswordResetTokenAsync` is cheap compared to PBKDF2, but the surrounding email-send must be fire-and-forget and identical in both branches.

**ModelState & request-body shape.** `[ApiController]` returns a `ValidationProblemDetails` 400 before the action executes when model binding fails. This bypasses the entire timing-mitigation path and produces a sub-millisecond response distinguishable from 401. This is acceptable because a 400 indicates malformed input, not "user exists" — but do not add custom server-side format checks (e.g. "is this a valid email syntax?") that only fire for the login action; they create a third timing class. Keep validation strictly client-side in attributes.

**Response body size.** The 401 `ProblemDetails` JSON must be byte-identical across all failure causes (unknown user, wrong password, locked out, 2FA required, not allowed). Any conditional detail field creates a content side-channel that renders timing mitigation moot.

**Normalization path.** `FindByEmailAsync` calls `ILookupNormalizer.NormalizeEmail` (default `UpperInvariantLookupNormalizer`) which does a `ToUpperInvariant` — O(n) in string length, but the input is bounded by the email max-length validator and executes in both the real and dummy paths (we call `FindByEmailAsync` first in both cases). No timing leak there.

## Testing the invariant in T02.5

Do **not** attempt to assert wall-clock timing parity in an xUnit/NUnit integration test. WebApplicationFactory-hosted tests run on the same process with a JIT that may inline on the second run, a thread-pool scheduler with millisecond-scale jitter, and no dedicated CPU — the 40 ms PBKDF2 signal is inside the noise floor of test-host variance. Every such assertion you have seen in blog posts is flaky.

Assert the **code path** instead. Two complementary tactics:

1. **Spy on `IPasswordHasher<AppUser>`.** Register a test double (e.g. via a `TestServer` `ConfigureTestServices`) that increments a counter on every `VerifyHashedPassword` / `HashPassword` call. Submit one `POST /api/auth/login` with a known-absent email and one with a known-present email. Assert the counter is `>= 1` in both cases. This proves the `user is null` branch executed a hash, which is the actual invariant you care about. The test is deterministic and runs in milliseconds.

2. **Statistical timing check as an opt-in benchmark, not a unit test.** If regulatory audit demands a numeric bound, add a `[Trait("Category", "Timing")]` test that issues N=200 requests for each case, computes median and p95, and asserts `|median(unknown) − median(known)| < 15 ms`. Gate it behind an env var so CI does not run it. BenchmarkDotNet is overkill for a two-condition comparison; a simple `Stopwatch` loop with warm-up iterations is sufficient. Record the raw distribution in a JSON artifact for the security-audit trail.

The first tactic belongs in T02.5. The second is a security-harness test that ships with the audit bundle, not the PR.

## Alternatives considered

**(A) Trust `SignInManager.PasswordSignInAsync` alone, rely on rate limiting.** Rejected — rate limiting caps attack throughput but does not change the timing signal; an enumerator with a single probe per minute still succeeds, just slowly. OWASP explicitly considers this insufficient.

**(B) Override `SignInManager<AppUser>` with a custom subclass that adds the dummy hash internally.** Considered. The subclass replaces `PasswordSignInAsync(string, …)` with a version that calls `PasswordHasher.VerifyHashedPassword(default!, DummyHash, password)` when `FindByNameAsync` returns null, then returns `SignInResult.Failed`. This is architecturally cleaner than controller-level mitigation and automatically covers any other caller of `PasswordSignInAsync` (e.g. a future Blazor page). Rejected for Slice 0 because the scope is a single controller and an override creates a maintenance burden that tracks Microsoft's internal method changes (e.g. the added `Stopwatch.GetTimestamp` / metrics calls in .NET 9). Revisit for MVP-2 if more login surfaces emerge.

**(C) Random-duration wrapper à la Cofoundry.** Wrap the entire login action in `Task.Delay(Random.Shared.Next(minMs, maxMs))` regardless of outcome. Rejected as the primary mechanism — it masks the symptom without fixing the cause, and the jitter window must exceed the PBKDF2 delta to be effective, which degrades login UX. Valuable as a *secondary* defense-in-depth for the lockout path in MVP-1 (see audit note above).

**(D) Pre-hash the password on the client.** Rejected — shifts the PBKDF2 cost to the browser, requires schema change to `AspNetUsers.PasswordHash`, incompatible with the stock `PasswordHasher` and with future passkey/WebAuthn migration. Out of scope.

**(E) Skip the lookup entirely and always call `CheckPasswordSignInAsync` on a sentinel user.** Rejected — `CheckPasswordSignInAsync` throws `ArgumentNullException` on null user and requires a real `TUser` instance to succeed; fabricating one with `new AppUser()` and calling `UserManager.CheckPasswordAsync` on it hits `PasswordHasher.VerifyHashedPassword(user, null, password)` which short-circuits and returns `Failed` without running PBKDF2. Doesn't achieve the goal.

## Decision-log entry draft

```
DEC-2026-04-22-001  AuthController.Login timing-safety mitigation
Status:   Accepted
Context:  Slice 0 §Unit 2 requires the login endpoint to reject invalid
          credentials with HTTP 401 and a generic ProblemDetails body
          without disclosing whether the email exists, including on the
          timing axis. The spec offers two implementation paths —
          SignInManager.PasswordSignInAsync, or FindByEmailAsync +
          dummy-hash — and leaves the choice to the implementer.

Decision: T02.4 implements the manual FindByEmailAsync + dummy-hash
          path. The dummy call is
          IPasswordHasher<AppUser>.VerifyHashedPassword(new AppUser(),
          DummyHash, request.Password) with DummyHash computed once at
          type initialization. SignInManager.PasswordSignInAsync is
          called only on the user-found branch. All failure modes
          (unknown user, wrong password, locked out, 2FA required, not
          allowed) collapse to an identical 401 + generic ProblemDetails
          body.

Rationale:
  1. Primary source evidence: dotnet/aspnetcore main
     src/Identity/Core/src/SignInManager.cs shows the string overload
     of PasswordSignInAsync returns SignInResult.Failed immediately
     when FindByNameAsync returns null, with no dummy-hash call. The
     timing delta between unknown-user and known-user-wrong-password
     paths is one PBKDF2-HMAC-SHA512 / 100 000-iteration pass
     (PasswordHasherOptions V3 defaults, unchanged since .NET 7),
     approximately 40–80 ms on server hardware — remotely observable.
  2. No merged PR closes this gap as of April 2026. Related issue
     #54542 (lockout-based enumeration) was closed as design-proposal
     without a framework fix. Microsoft treats enumeration hardening
     as application responsibility.
  3. OWASP Authentication Cheat Sheet, ASVS v5 §6.2.3, and WSTG
     account-enumeration guidance all require uniform failure timing
     and collapse of failure modes; option (A) "trust PasswordSignInAsync"
     does not satisfy these requirements.
  4. VerifyHashedPassword(default, DummyHash, password) was chosen over
     HashPassword(default, password) because it traverses the same code
     path as real-user failure (parse header → derive key → constant-time
     compare), giving tighter timing parity at the same PBKDF2 cost.
     DummyHash is cached statically to avoid paying an extra PBKDF2 on
     every unknown-user request, which would flip the timing leak.

Consequences:
  + Timing side-channel closed for the unknown-user axis under current
    Slice 0 configuration (lockoutOnFailure: false).
  + Reusable pattern documented for MVP-1 registration and
    password-reset endpoints.
  − Lockout path (MVP-1) will re-introduce a secondary timing asymmetry
    (DB write on AccessFailedAsync) that this mitigation does not close;
    revisit with a uniform-delay envelope or a SignInManager subclass
    when lockoutOnFailure is enabled.
  − Maintenance burden: if Microsoft eventually adds built-in
    timing-safety to SignInManager (no such PR tracked), remove the
    manual mitigation and switch to the string overload. Track via a
    TODO tagged with this decision ID.

Test coverage (T02.5):
  Assert that IPasswordHasher<AppUser>.VerifyHashedPassword is invoked
  at least once for both the unknown-user and known-user-wrong-password
  request cases. Implementation via a counting test double registered
  through WebApplicationFactory.ConfigureTestServices. Wall-clock
  timing assertions are explicitly out of scope — they belong in the
  security-audit benchmark harness, not in the integration suite.
```

## Conclusion

The spec's "or" clause is resolved by .NET 10 source code: option A is not actually an option. `SignInManager.PasswordSignInAsync` still exits microseconds-fast on the unknown-user branch in April 2026 `main`, a PBKDF2-HMAC-SHA512 / 100k-iteration pass separates it from the known-user branch, and neither Microsoft nor the default scaffolded templates ship a mitigation. T02.4 must implement the `FindByEmailAsync` + cached-`VerifyHashedPassword` dummy-hash pattern shown above, collapse all failure modes into one generic 401, and document that the lockout path re-opens the leak on a secondary axis that MVP-1 must address. T02.5 asserts the *invariant* (hash-was-called) via a counting test double, not wall-clock parity. The manual mitigation is also the correct template for the upcoming registration and password-reset endpoints.