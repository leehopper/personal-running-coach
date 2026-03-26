# T11 Proof Summary: Add test for negative TimeSpan in VdotCalculator

| # | Type | Artifact | Status |
|---|------|----------|--------|
| 1 | Test | `dotnet test` -> 314 passed, 0 failed (1 new negative TimeSpan case) | PASS |
| 2 | CLI | `dotnet build` -> Build succeeded, 0 warnings, 0 errors | PASS |

All 2 proof artifacts passed. Converted `CalculateVdot_ZeroTime_ReturnsNull` from
a `[Fact]` to a `[Theory]` with `[InlineData(0)]` and `[InlineData(-30)]`, covering
both branches of the `timeInMinutes <= 0` guard at VdotCalculator.cs line 44.
The zero case preserves existing coverage; the negative case is the net-new addition.
