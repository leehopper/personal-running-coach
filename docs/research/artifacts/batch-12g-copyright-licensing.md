# Daniels pace tables: what RunCoach can safely commit

**Independently computed values from the public-domain Daniels-Gilbert equations are safe to commit as test fixtures in any repository, public or private.** The 1979 regression equations are mathematical formulas explicitly excluded from copyright under 17 U.S.C. § 102(b), and their numerical outputs are uncopyrightable mathematical facts under *Feist v. Rural Telephone*. The book-transcribed lookup table currently in the codebase — whose row-shift errors confirm manual transcription from the copyrighted 4th edition — should be replaced with equation-computed values before any open-sourcing. This approach eliminates legal risk entirely, aligns with how every major open-source project (GoldenCheetah, tlgs/vdot) handles this problem, and is already consistent with RunCoach's planned migration to equation-computed values.

## The equations are free; the tables carry thin copyright

U.S. copyright law draws a bright line between mathematical formulas and their creative presentation. The Daniels-Gilbert oxygen cost equations — first published in Daniel's 1978 John Wiley & Sons text and formalized in the 1979 *Oxygen Power* monograph — are unambiguously uncopyrightable. The Copyright Office's Compendium (Third) § 313.3(A) explicitly excludes "mathematical principles, formulas, algorithms, or equations" from protection. The two core regression formulas (VO2 cost as a function of velocity, and fractional utilization as a function of time) with their exact coefficients (−4.60, 0.182258, 0.000104, 0.8, 0.1894393, 0.012778, 0.2989558, 0.1932605) are widely published across academic sources, running forums, and open-source codebases.

The book's pace tables occupy a different legal position, though their protection is surprisingly narrow. Under 17 U.S.C. § 103, compilations receive copyright only in their **selection, coordination, and arrangement** — never in the underlying data. The Supreme Court's landmark *Feist Publications v. Rural Telephone Service* (499 U.S. 340, 1991) rejected the "sweat of the brow" doctrine and held that copyright "rewards originality, not effort." Daniels' tables do contain some arguably creative choices: which VDOT values to include, which training pace categories to define (Easy, Marathon, Threshold, Interval, Repetition), and how to round. This gives the tables at most a "thin" compilation copyright protecting the specific table layout, not the numerical values themselves.

A critical distinction from *CCC Information Services v. Maclean Hunter* (44 F.3d 61, 2d Cir. 1994) strengthens this position. That court found the Red Book's used-car valuations copyrightable precisely because they were **not mechanically derived from formulas** but reflected "editors' predictions" and "professional judgment." Daniels' pace values, by contrast, are deterministic outputs of known public-domain equations — closer to the uncopyrightable facts in *Feist* than to the subjective predictions in *CCC*.

## Three scenarios carry very different legal weight

The legal risk depends entirely on *how* values are obtained and *what* is actually committed:

**Scenario A — Reproducing the book's table verbatim** carries moderate risk. The current RunCoach lookup table, with its telltale row-shift transcription error in VDOT 50–85, is strong circumstantial evidence of copying from the copyrighted 4th edition. Committing this in a public repository would reproduce the protected arrangement and create an indefensible provenance chain. Even though individual values are facts, wholesale reproduction of the specific table structure could infringe the thin compilation copyright.

**Scenario B — Independently computing values from the DG equations** is the safe harbor. Independent creation is a complete defense to copyright infringement. As Judge Learned Hand wrote in *Fred Fisher v. Dillingham* (298 F. 145, S.D.N.Y. 1924): "The law imposes no prohibition upon those who, without copying, independently arrive at the precise combination." When values are deterministic outputs of public-domain equations, independent derivation is trivially demonstrable. The *merger doctrine* provides additional protection: when an equation can produce only one correct answer for a given input, the expression merges with the idea and cannot be copyrighted (*Ho v. Taflove*, 648 F.3d 489, 7th Cir. 2011).

**Scenario C — Computing values that differ slightly due to rounding** is even safer. Independent rounding choices affirmatively prove the values were not copied. This is the strongest possible position.

## How the open-source community actually handles this

A survey of existing projects reveals a clear consensus: **serious open-source projects implement the equations; legally naive ones commit the tables.**

**GoldenCheetah** — the most prominent open-source running/cycling analytics tool (~2,100 GitHub stars, GPL v2) — represents the gold standard. Its `VDOTCalculator.cpp` implements the DG equations directly in C++ with Newton-Raphson solvers for race time prediction. It stores **zero lookup table data** and cites its formula source in code comments. All training paces are computed dynamically. This is the model RunCoach should follow.

**tlgs/vdot** (0BSD license) takes a similar equation-first approach through Jupyter notebooks, explicitly positioning itself as "an exploration of the math." Its README notes that Daniels and the Run SMART Project have "taking down 3rd party calculators" — plural — confirming multiple enforcement actions. The **vdot-calculator** package on PyPI (MIT license) also computes purely from equations.

On the other side, projects like **ericgio/vdot** and **christoph-phillips/daniels-calculator** appear to commit hardcoded lookup tables with no copyright disclaimers or licenses — precisely the approach that creates legal exposure.

The **fellrnr.com** case is instructive. The site's VDOT Calculator page now reads: "Because Jack Daniels requested the removal of any VDOT functionality, the calculator has been moved to Running Calculator." This was **not a formal DMCA takedown** but a direct personal request from Daniels. Notably, fellrnr retained substantial descriptive content about VDOT — the request specifically targeted calculator functionality, not discussion of the concept. This pattern suggests Daniels' enforcement focuses on tools that compete with the official vdoto2.com calculator, particularly those using his branding.

## The vdoto2.com terms prohibit scraping but not independent computation

The Run SMART Project's Terms of Service (revised January 1, 2026) at vdoto2.com/terms explicitly prohibit automated scraping: Section 11(vii) bars any "manual or automated software, devices, or other processes to 'crawl,' 'scrape,' or 'spider' any page of the Services." The ToS broadly claims all content is proprietary under Section 10, though this is standard boilerplate whose enforceability against mathematical facts is doubtful under copyright law.

**Manual use of the free calculator is the intended use case** and is clearly permitted. However, sourcing test fixture values from vdoto2.com is unnecessary and creates an avoidable provenance complication. The vdoto2.com calculator may also use modified or updated formulas beyond the published DG equations — one LetsRun discussion noted discrepancies between the online calculator and book tables for low VDOT values. Values sourced from the calculator would thus have ambiguous provenance: potentially derived from proprietary modifications rather than the public-domain equations.

**"VDOT" is a registered trademark** of The Run SMART Project, LLC. RunCoach should use the term descriptively with appropriate attribution and avoid implying endorsement.

## The equation-derived fixture strategy eliminates legal risk

The recommended approach layers four independent legal protections, any one of which would likely suffice alone:

**First, the values are uncopyrightable mathematical facts.** The outputs of deterministic public-domain equations are facts, not creative expression. Under *Feist*, "the raw facts may be copied at will." Under *Baker v. Selden* (101 U.S. 99, 1879), copyright in a work describing a method does not grant exclusive rights over the method itself. Under *Assessment Technologies v. WIREdata* (350 F.3d 640, 7th Cir. 2003), Judge Posner warned that attempting to use compilation copyright to "sequester" uncopyrightable data may constitute **copyright misuse**.

**Second, independent derivation negates copying.** Copyright infringement requires actual copying. Implementing equations from the 1979 monograph and computing values produces independently derived results, regardless of whether they match the book. A documented derivation chain (monograph → code → computed values) establishes this conclusively.

**Third, even if compilation copyright existed, fair use would apply.** A test fixture is transformative (software validation, not coaching), the work is factual, selected sample values represent a small portion, and a CSV in a test suite has zero market substitution effect for a $25 coaching book. Under *Andy Warhol Foundation v. Goldsmith* (2023), the "distinct purpose" test clearly separates software testing from running instruction.

**Fourth, the merger doctrine prevents monopolization.** When only one set of correct values can be produced by applying specific equations to specific inputs, expression merges with idea and copyright cannot attach (*Ho v. Taflove*, 7th Cir. 2011).

## Recommendation: what to commit and how

RunCoach should adopt the following approach, which eliminates all meaningful legal risk:

**Replace the book-transcribed lookup table immediately.** The current table with its VDOT 50–85 row-shift error is demonstrably copied from the copyrighted 4th edition. This should not be committed to any public repository. Replace it with equation-computed values as already planned.

**Generate golden test fixtures from the DG equations using this process:**
1. Implement the two DG regression equations from the 1979 *Oxygen Power* monograph (not from the book's tables)
2. Make independent rounding decisions — document your precision and rounding convention explicitly
3. Compute fixture values at representative VDOT levels (e.g., 30, 35, 40, 45, 50, 55, 60, 65, 70) rather than reproducing the entire table's range and structure
4. Use your own training-pace percentage derivations where possible, or cite the published literature for standard percentages (e.g., threshold at ~88% vVDOT)

**Include provenance metadata in the fixture file.** A header comment like:

```
# Generated from Daniels-Gilbert oxygen cost equations
# Source: Daniels, J. & Gilbert, J. (1979). Oxygen Power: Performance
#   Tables for Distance Runners. Tempe, AZ.
# Equations: VO2 = -4.60 + 0.182258*v + 0.000104*v^2
#   %VO2max = 0.8 + 0.1894393*e^(-0.012778*t) + 0.2989558*e^(-0.1932605*t)
# Values independently computed; not transcribed from any published table.
# VDOT is a trademark of The Run SMART Project, LLC.
# This project is not affiliated with or endorsed by The Run SMART Project
#   or Dr. Jack Daniels.
```

**Add a trademark disclaimer** in the README or relevant documentation acknowledging that VDOT is a registered trademark of The Run SMART Project, LLC, and that RunCoach is not affiliated with or endorsed by Daniels or TRSP. Use the term "VDOT" descriptively rather than as branding.

**Do not scrape vdoto2.com** for calibration values. This violates their ToS and creates unnecessary provenance issues. If you want external validation, compare your equation outputs against the numerous independent open-source implementations (GoldenCheetah's `VDOTCalculator.cpp` is the most trustworthy reference) or against values published in academic running science literature.

**What is safe to commit in a public repository:**
- The DG equation implementations in code — **yes, unambiguously safe**
- Fixture values independently computed from those equations — **yes, safe**
- The specific table layout and training-pace category structure from the book — **avoid reproducing this structure**
- Book-transcribed CSV of pace values — **no, do not commit**
- A small number of spot-check values for cross-validation — **yes, fair use even if sourced from published tables, but better to derive independently**

The equation-derived approach is not merely a legal workaround — it is the technically superior solution. It eliminates the row-shift transcription error already discovered, allows arbitrary precision and VDOT granularity beyond the book's integer increments, and enables RunCoach to compute values for any distance rather than only the distances Daniels chose to tabulate. Every serious open-source project in this space has reached the same conclusion.