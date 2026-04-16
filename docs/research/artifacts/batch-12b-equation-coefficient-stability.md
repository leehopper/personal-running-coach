> **User-facing rename (2026-04-15):** References to "VDOT" in this document are preserved as historical technical vocabulary. Per DEC-043, the project's user-facing surface now uses "Daniels-Gilbert zones" or "pace-zone index" terminology to avoid exposure to The Run SMART Project LLC trademark (Runalyze enforcement precedent). Artifact body retained as-is for research provenance.

# Daniels-Gilbert equation coefficients: stable since 1979 but never fully verifiable

The eight coefficients in the Daniels-Gilbert VDOT equations have remained **identical across every known source** — from community GitHub implementations to academic papers to online calculators — with no evidence of revision across four decades and four book editions. However, a critical verification gap persists: **no edition of *Daniels' Running Formula* actually prints the mathematical equations**, and the 1979 source monograph has never been digitized. This means the coefficients cannot be confirmed by direct inspection of any currently available authoritative publication, including the 4th edition (2022). For software implementation purposes, the practical risk of divergence is near zero, but the epistemic status is "universally consistent across derivative sources" rather than "verified against primary text."

---

## The 1979 origin and its inaccessible primary source

The equations trace to **"Oxygen Power: Performance Tables for Distance Runners"** (1979), a self-published, spiral-bound pamphlet of approximately 107 pages by Jack Daniels and Jimmy Gilbert, produced in Tempe, Arizona (OCLC 30094144). Gilbert was a NASA mathematician who spent 35 years building Apollo spacecraft simulations — he translated Daniels' physiological data into regression equations. According to Amby Burfoot, who photographed the original pamphlet, the book was "primarily 82 pages of pure numbers (VDOTs, distances, and times)."

The oxygen cost equation actually predates *Oxygen Power* by one year. It first appeared in Daniels' contribution to *The Conditioning for Distance Running — the Scientific Aspects* (John Wiley & Sons, 1978). The fractional utilization equation — what Larry Simpson calls the "drop dead formula" — appears to be original to the 1979 monograph. Simpson, who purchased the pamphlet circa 1980 from a *Runner's World* footnote offer, states: "I have not seen the 'drop dead' formula published any other time."

**No digitized version of *Oxygen Power* exists.** Google Books lists it (ID: h7f_tgAACAAJ) with no preview. C.D. Chester's 2021 academic paper pinpoints the equations to pages 97 and 99 of the monograph but notes: "Legally, I can't put either of the equations in this paper directly" — suggesting they may be considered proprietary. This means direct visual comparison against the original text requires physical access to one of the surviving copies.

## None of the four book editions print the formulas

This is the single most important finding for verification purposes: **none of the four editions of *Daniels' Running Formula* (1998, 2005, 2014, 2022) publish the mathematical equations.** Every edition provides only the computed VDOT lookup tables and training pace charts.

Larry Simpson confirmed this explicitly for the 1st edition: "Don't be misled by the title of Daniels' book. If you purchase it, don't expect to find his oxygen cost or 'drop dead' formulae spelled out mathematically. He only gives you the tables generated from the formulae." The word "formula" in the title refers to Daniels' training methodology, not a mathematical formula.

For the **4th edition** (2022, Human Kinetics, ISBN 978-1718203662) — the highest-priority verification target — the table of contents shows Chapter 5 ("VDOT System of Training") and an appendix for "Time and Pace Conversions," but **no equation appendix**. No review, preview, excerpt, or discussion of the 4th edition mentions the mathematical equations being printed. The preface states: "You might find it strange that after so many years of study and coaching, I keep finding new, more practical, and often simpler ways of prescribing training and racing programs" — language pointing to training refinements, not equation changes.

Edition-to-edition comparison of the VDOT tables themselves provides strong indirect evidence of coefficient stability. Fellrnr.com explicitly confirmed that between the 2nd and 3rd editions, **"the table of race performance to VDOT has not changed at all"** and "paces for T, I, and R have not changed." The 3rd edition added tables for slower runners (VDOT 20–30) and widened the Easy pace range. The 4th edition added age-related modifications and ultra/triathlon content. No source documents changes to the core VDOT-to-performance tables between the 3rd and 4th editions.

## Coefficient consistency across all derivative sources

Every implementation that documents coefficients uses **exactly the same eight values**:

| Coefficient | Value | Equation | Role |
|---|---|---|---|
| Intercept | −4.60 | Oxygen cost | Constant offset |
| Linear term | 0.182258 | Oxygen cost | Velocity coefficient |
| Quadratic term | 0.000104 | Oxygen cost | Velocity² coefficient |
| Constant | 0.8 | Fractional utilization | Asymptotic baseline |
| 1st exponential amplitude | 0.1894393 | Fractional utilization | Slow-decay component |
| 1st decay rate | 0.012778 | Fractional utilization | Slow-decay rate |
| 2nd exponential amplitude | 0.2989558 | Fractional utilization | Fast-decay component |
| 2nd decay rate | 0.1932605 | Fractional utilization | Fast-decay rate |

These coefficients were verified as identical in: GoldenCheetah's C++ implementation (VDOTCalculator.cpp), the mekeetsa/vdot GitHub repository (citing both *Oxygen Power* and *Daniels' Running Formula* 2nd edition), the tlgs/vdot Python project, the lsolesen/running-calculations PHP library, sport-calculator.com, running-calculator.com, rundida.com, runbundle.com, runregimen.com, had2know.org, and multiple LetsRun forum threads. **Zero variant coefficients were found in any source.**

All roads trace back to Larry Simpson's simpsonassociatesinc.com, which is the earliest web documentation of the formulas and the most-cited source by implementers. Simpson obtained the equations directly from his copy of the 1979 pamphlet.

## The vdoto2.com calculator remains a black box

The official VDOT calculator at vdoto2.com (operated by the Run SMART Project, LLC) **does not publicly document its formula coefficients** anywhere — not on the calculator page, not in an FAQ, and not in accessible source code. The calculator uses a JavaScript-based interactive interface without exposing the underlying math.

Indirect verification is possible through output comparison. A LetsRun poster who contacted the Run SMART team reported being told the online calculator uses "the calculations from the original publication" without rounding to integer VDOT increments, which accounts for minor discrepancies between the calculator and the book tables. For high VDOT values, outputs align well with the 1979 equations. For very low VDOT values (below ~30), the tlgs/vdot project noted the official calculator "recommends non-sensical training paces," suggesting possible edge-case issues in the training-pace mapping layer rather than in the underlying VDOT equation.

The V.O2 News blog (news.vdoto2.com) documents feature additions like temperature and altitude adjustments but **never references the underlying equation coefficients** or any changes to them.

## No errata, no announced revisions, no public discussion of changes

**Human Kinetics errata:** No public errata page was found for any edition of *Daniels' Running Formula* on the Human Kinetics website. The publisher does not appear to maintain publicly accessible errata for individual titles.

**Known table errors:** The tlgs/vdot project (July 2023) documented errors in the 3rd edition's printed tables. A LetsRun "math major" who reverse-engineered the equations found minor discrepancies: "I calculate the mile time for a VDOT of 30 as 9:10.37, or 9:10, but the table indicates 9:11" and noted the 800m time for VDOT 72 was off by 2 seconds. These appear to be rounding choices or manual adjustments by Daniels rather than evidence of different underlying equations.

**Daniels on equation updates:** Jack Daniels (1933–2025) never publicly discussed updating the VDOT equation coefficients in any accessible interview, Q&A session, book preface, blog post, podcast appearance, or coaching material. His "Ask Dr. Jack Daniels" column on the V.O2 blog addressed VDOT usage and training philosophy but never the mathematical foundations. On CoachesEducation.com (November 2000), Daniels described VDOT using "standard values for running economy" — language suggesting fixed, established equations. **The topic of coefficient revision simply does not appear in the public record.**

Daniels did actively control use of the VDOT brand. Fellrnr noted that Daniels "requested the removal of any VDOT functionality" from Fellrnr's calculator around 2015, and the tlgs/vdot project observed Daniels had been "making an effort to keep its formula secret and taking down 3rd party calculators." This intellectual property posture is consistent with treating the equations as a settled, proprietary asset rather than an evolving model.

## What this means for software implementations

For the VdotCalculator and PaceCalculator classes that depend on these coefficients, five conclusions emerge:

**The coefficients are safe to use.** Every known implementation uses identical values, the VDOT tables are confirmed stable across book editions, and no evidence of revision exists. The practical risk of a silent coefficient change is negligible.

**Direct verification against the 4th edition is impossible.** The equations are not printed in any edition. Verification would require either (a) obtaining a physical copy of the 1979 monograph, (b) reverse-engineering the vdoto2.com calculator's JavaScript, or (c) systematic output comparison against the official calculator across many input values.

**Minor table discrepancies are a rounding issue, not an equation issue.** The 1–2 second differences between computed race times and printed table values reflect Daniels' rounding to whole seconds and integer VDOT increments, not different underlying mathematics.

**Edge cases below VDOT ~30 may behave differently in the official calculator.** The training-pace mapping layer (which converts VDOT to specific Easy, Tempo, Interval, and Repetition paces) may apply adjustments for very slow runners that are not captured by the raw equations. This is a training-pace concern, not a VDOT-calculation concern.

**The coefficients' provenance is a chain of trust, not direct verification.** Every online source traces ultimately to Simpson's web documentation of the 1979 pamphlet, or to independent extraction from other copies of the same pamphlet. The consistency across independent extractions (Simpson, multiple LetsRun users, academic citations referencing pp. 97 and 99) provides high confidence but not cryptographic certainty. For a software implementation, this level of confidence is more than sufficient.