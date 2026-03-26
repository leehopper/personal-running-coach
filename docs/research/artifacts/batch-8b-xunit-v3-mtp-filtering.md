# Fixing xUnit v3 trait filtering and coverage under MTP

**The fix is straightforward: use xUnit v3's native `--filter-not-trait` flag instead of VSTest's `--filter`, and replace `coverlet.msbuild` with `coverlet.MTP`.** Your current `coverlet.msbuild` package is fundamentally broken under MTP because the MSBuild target it hooks into (`BuildProject`) no longer exists in MTP's build flow. Meanwhile, MTP completely ignores VSTest's `--filter` syntax by design — xUnit v3 implements its own filtering extensions within MTP. The combination of these two issues requires a small but specific set of changes to your project and CI pipeline.

---

## The correct filter syntax for xUnit v3 on MTP

xUnit v3 does **not** use MTP's generic `--treenode-filter` (that's for MSTest/TUnit). Instead, it registers its own CLI extensions within the MTP framework. The flag you need is `--filter-not-trait`:

```bash
# .NET 10 SDK with global.json MTP mode (no -- separator needed)
dotnet test --filter-not-trait "Category=Eval"

# .NET 9 SDK with TestingPlatformDotnetTestSupport=true (-- separator required)
dotnet test -- --filter-not-trait "Category=Eval"
```

The full set of xUnit v3 MTP filter switches includes `--filter-trait`, `--filter-not-trait`, `--filter-class`, `--filter-not-class`, `--filter-method`, `--filter-not-method`, `--filter-namespace`, `--filter-not-namespace`, and `--filter-query` for the advanced query language. Multiple values combine with AND logic: `--filter-not-trait "Category=Eval" "Category=Slow"` excludes both. Wildcards (`*`) work at start/end of values. You **cannot mix** simple filters with `--filter-query`.

For more complex expressions, xUnit v3's query filter language offers `/[Category!=Eval]` syntax via `--filter-query "/[Category!=Eval]"`. The query language supports segments (`/assembly/namespace/class/method`), trait conditions in brackets, combinators (`&`, `|`), and negation (`!`).

**Why your previous attempts failed:** `dotnet test --filter "Category!=Eval"` uses VSTest syntax, which MTP ignores (hence MTP0001). The `-trait-` flag is xUnit's native CLI syntax, not its MTP CLI syntax — passing `-trait- "Category=Eval"` through the MTP bridge doesn't work because MTP expects `--filter-not-trait` instead. And `/p:TestingPlatformDotnetTestSupport=false` can't fall back to VSTest cleanly because .NET 10's `dotnet test` has been rearchitected around MTP.

---

## Replace coverlet.msbuild with coverlet.MTP

**`coverlet.msbuild` is confirmed broken under MTP** (coverlet issue #1715). The root cause: MTP's build flow uses `_BuildAndInvokeTestingPlatform` → `InvokeTestingPlatform` targets, completely bypassing the `BuildProject` target that coverlet.msbuild hooks into for instrumentation. No instrumentation means no coverage data. The `coverlet.collector` package (VSTest data collector) is equally broken since MTP ignores `--collect` flags.

The solution is `coverlet.MTP`, a native MTP extension released in **coverlet 8.0.0** (January 2026). It implements coverlet's instrumentation as an MTP extension, activated with the `--coverlet` flag:

```bash
# Remove coverlet.msbuild, add coverlet.MTP
dotnet remove package coverlet.msbuild
dotnet add package coverlet.MTP
```

Combined with trait filtering, your CI command becomes:

```bash
dotnet test --filter-not-trait "Category=Eval" \
  --coverlet \
  --coverlet-output-format cobertura \
  --ignore-exit-code 8
```

The `--ignore-exit-code 8` flag is important: when filtering excludes all tests in a project, MTP returns exit code 8 ("zero tests ran"), which CI treats as failure. This flag suppresses that specific exit code.

An alternative to coverlet.MTP is **`Microsoft.Testing.Extensions.CodeCoverage`**, the official Microsoft solution recommended by the xUnit team. However, there's a critical version compatibility constraint: xUnit v3 **3.2.2** bundles MTP 1.x, so you must use CodeCoverage **v18.0.x** (not v18.4.x+, which requires MTP 2.x and will throw `System.TypeLoadException`). Given this fragility, `coverlet.MTP` is the simpler choice if you're already familiar with Coverlet.

---

## Recommended csproj and CI changes

For .NET 10, migrate from `TestingPlatformDotnetTestSupport` (now obsolete) to the `global.json` approach. This eliminates the `--` separator requirement and aligns with the .NET 10 SDK's native MTP support:

**global.json** (add or update at repo root):
```json
{
  "sdk": { "version": "10.0.100-preview.3" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

**Test project .csproj** changes:
```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <!-- Remove these obsolete properties in .NET 10 MTP mode -->
  <!-- <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport> -->
  <!-- <TestingPlatformShowTestsFailure>true</TestingPlatformShowTestsFailure> -->
</PropertyGroup>

<ItemGroup>
  <!-- Keep xUnit v3 -->
  <PackageReference Include="xunit.v3" Version="3.2.2" />
  
  <!-- REMOVE coverlet.msbuild, ADD coverlet.MTP -->
  <!-- <PackageReference Include="coverlet.msbuild" Version="..." /> -->
  <PackageReference Include="coverlet.MTP" Version="8.0.1" />
</ItemGroup>
```

If you want trait exclusion baked into the project for CI without passing CLI args, use the `TestingPlatformCommandLineArguments` MSBuild property:

```xml
<PropertyGroup>
  <!-- Exclude eval tests when CI=true (GitHub Actions sets this automatically) -->
  <TestingPlatformCommandLineArguments 
    Condition="'$(CI)' == 'true'">$(TestingPlatformCommandLineArguments) --filter-not-trait Category=Eval --ignore-exit-code 8</TestingPlatformCommandLineArguments>
</PropertyGroup>
```

This property appends arbitrary arguments to the test executable invocation. It works with both `dotnet test` and is the cleanest way to embed CI-specific filtering without modifying the CI workflow file.

---

## GitHub Actions workflow

Here's the complete CI step configuration:

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
          dotnet-quality: 'preview'
      
      # Main test step — excludes Eval tests, collects coverage
      - name: Run tests
        run: |
          dotnet test \
            --filter-not-trait "Category=Eval" \
            --coverlet \
            --coverlet-output-format cobertura \
            --coverlet-output ./coverage/coverage.cobertura.xml \
            --ignore-exit-code 8
      
      # Eval tests — separate step with cache replay
      - name: Run eval tests
        env:
          EVAL_CACHE_MODE: Replay
        run: |
          dotnet test \
            --filter-trait "Category=Eval"
      
      # Upload coverage (optional)
      - name: Upload coverage
        uses: codecov/codecov-action@v4
        with:
          files: ./coverage/coverage.cobertura.xml
```

If you prefer the MSBuild property approach instead of CLI flags, the test step simplifies to just `dotnet test --coverlet --coverlet-output-format cobertura` since the filtering is embedded in the project file conditioned on `$(CI)`.

---

## What about staying on VSTest?

xUnit v3 **does** still support VSTest via `xunit.runner.visualstudio` (v3.0.0+), and this would let you keep `coverlet.msbuild` with the familiar `--filter "Category!=Eval"` syntax. However, there are three reasons not to go this route. First, .NET 10's `dotnet test` has been fundamentally rearchitected around MTP — VSTest compatibility is a legacy shim, not the primary path. Second, you'd need to remove the MTP configuration entirely and ensure `xunit.runner.visualstudio` is referenced, adding friction if you later want MTP features. Third, the `xunit.runner.json` configuration file **does not support trait filtering** at all (it handles parallelism, display options, and diagnostics only), so there's no runner-config-based alternative regardless of which adapter you use.

The `coverlet.MTP` + `--filter-not-trait` combination is the path of least resistance: it keeps you on MTP, works natively with `dotnet test` in .NET 10, and requires only a NuGet package swap and a one-line CI command change.

---

## Conclusion

The core issue was a syntax mismatch at two levels: VSTest filter syntax being ignored by MTP, and coverlet.msbuild hooking into MSBuild targets that MTP bypasses. The fix requires **`--filter-not-trait "Category=Eval"`** for filtering (xUnit v3's own MTP extension, not VSTest's `--filter`) and **`coverlet.MTP`** for coverage (a native MTP extension replacing the broken `coverlet.msbuild`). For .NET 10, drop `TestingPlatformDotnetTestSupport` from your csproj in favor of `global.json`'s `"test": { "runner": "Microsoft.Testing.Platform" }`, which eliminates the `--` separator requirement. Add `--ignore-exit-code 8` to handle projects where filtering zeroes out all tests. The `TestingPlatformCommandLineArguments` MSBuild property offers an elegant way to embed CI-specific filtering directly in the project, conditioned on the `$(CI)` environment variable that GitHub Actions sets automatically.