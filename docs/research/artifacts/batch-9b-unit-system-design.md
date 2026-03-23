# A clean unit system for your RunCoach app

**Store everything in meters and seconds-per-kilometer internally, convert only at display boundaries, and never let the LLM do math.** This is the universal pattern across Strava, Garmin, and TrainingPeaks — all three store and serve data in fixed SI-metric units regardless of user preferences — and it eliminates the entire class of bugs that destroyed NASA's $327 million Mars Climate Orbiter. For your .NET app, the right move is custom `readonly record struct` value objects for `Distance` and `Pace` (since no library provides a running-specific pace type), with conversions happening exclusively at the API boundary and in the context assembly layer that feeds your LLM. The phased approach is straightforward: build the value objects and canonical storage now for MVP-0, add a `UnitPreference` enum and formatting layer for MVP-1, and defer granular per-context unit preferences until user feedback demands it.

---

## Every major running platform stores in meters internally

The industry consensus is unambiguous. **Strava's API returns all distances in meters and all speeds in meters per second**, regardless of the user's display preference. Garmin's Health API goes further with self-documenting field names like `distanceInMeters` and `averageSpeedInMetersPerSecond` — the unit is baked into the field name itself. FIT files (the standard wearable data format) store distance as unsigned 32-bit integers with centimeter precision. TrainingPeaks ingests FIT files directly and stores metric internally.

The data flow from wrist to cloud follows the same pattern everywhere: GPS sensor measures raw coordinates → watch firmware computes meters and m/s → display layer converts to the user's preferred unit for on-screen rendering → FIT file records metric values → cloud sync transfers metric → API exposes metric → consuming application converts for display. Your Garmin watch showing "3.11 mi" is a presentation-layer conversion of the 5000 meters stored in the FIT file.

Apple HealthKit is the lone exception to the "fixed canonical unit" pattern. It stores data as `HKQuantity` objects — a double value paired with an `HKUnit` — and converts on read via `doubleValue(for:)`. You can write in miles and read back in kilometers seamlessly. This is elegant but adds complexity that the other platforms avoid by simply standardizing on meters.

**The practical implication for RunCoach**: when ingesting from any wearable API, you'll receive metric data. The user's watch display settings are irrelevant to your data pipeline. No conversion is needed at ingest for Strava or Garmin data. For HealthKit, request values in meters explicitly using `doubleValue(for: .meter())`.

---

## The recommended .NET type system: value objects over library dependencies

For a running app that needs exactly three measurement types — distance, pace, and duration — **custom `readonly record struct` value objects are the right choice**, optionally backed by UnitsNet for distance conversions only. UnitsNet (19.25 million NuGet downloads, MIT-0 licensed, actively maintained) provides rock-solid `Length` and `Speed` types with 1,200+ units, but it has no concept of pace (time per unit distance), which is the single most important measurement in running coaching.

Here's the recommended type design:

```csharp
public readonly record struct Distance(double Meters) : IComparable<Distance>
{
    public double Kilometers => Meters / 1000.0;
    public double Miles => Meters / 1609.344;

    public static Distance FromMeters(double m) => new(m);
    public static Distance FromKilometers(double km) => new(km * 1000.0);
    public static Distance FromMiles(double mi) => new(mi * 1609.344);

    public static Distance operator +(Distance a, Distance b) => new(a.Meters + b.Meters);
    public int CompareTo(Distance other) => Meters.CompareTo(other.Meters);
}
```

```csharp
public readonly record struct Pace : IComparable<Pace>
{
    public double SecondsPerKm { get; }

    private Pace(double secondsPerKm)
    {
        if (secondsPerKm <= 0) throw new ArgumentOutOfRangeException(nameof(secondsPerKm));
        SecondsPerKm = secondsPerKm;
    }

    public static Pace FromSecondsPerKm(double s) => new(s);
    public static Pace FromMinutesPerMile(double m) => new(m * 60.0 / 1.609344);
    public static Pace FromDistanceAndTime(Distance d, TimeSpan t) => new(t.TotalSeconds / d.Kilometers);

    public double SecondsPerMile => SecondsPerKm * 1.609344;
    public double KilometersPerHour => 3600.0 / SecondsPerKm;

    public bool IsFasterThan(Pace other) => SecondsPerKm < other.SecondsPerKm;
    public bool IsSlowerThan(Pace other) => SecondsPerKm > other.SecondsPerKm;
    public int CompareTo(Pace other) => SecondsPerKm.CompareTo(other.SecondsPerKm);
}
```

**Why `double` and not `decimal`?** Running distances are physical measurements with inherent GPS imprecision (±3–10 meters). `double` provides ~15 digits of precision — enough to represent a marathon distance (42,195 meters) down to nanometers. The performance advantage of `double` matters for VDOT calculations and aggregate statistics. Reserve `decimal` for money.

**Why `readonly record struct`?** Immutability prevents accidental mutation. Value semantics ensure `Distance.FromMiles(1.0) == Distance.FromKilometers(1.609344)` returns `true` — because they resolve to the same internal meters value. Stack allocation means zero GC pressure. Built-in `Equals`, `GetHashCode`, and `ToString` come free.

A critical design note on the `Pace` type: **do not implement `<` and `>` operators**. Faster pace means a *lower* number (4:00/km is faster than 5:00/km), which makes comparison operators semantically confusing. Instead, provide explicit `IsFasterThan()` and `IsSlowerThan()` methods that make the intent unambiguous at every call site. Similarly, `PaceRange` should use `Fast` and `Slow` field names rather than `Min` and `Max`.

---

## Race distances are concepts, not just numbers

A "5K" is simultaneously a distance (5,000 meters) and a cultural concept — a race category with specific pacing strategies, training approaches, and VDOT tables. **Model standard race distances as an enum with associated canonical distances**, and support custom distances for non-standard events:

```csharp
public enum StandardRace
{
    FiveK,        // 5000m
    TenK,         // 10000m
    HalfMarathon, // 21097.5m (exact: 21.0975 km)
    Marathon      // 42195m (exact: 42.195 km, IAAF-defined)
}
```

This matters because "5K" should never become "3.1 miles" or "5.0 kilometers" in coaching output — it's a proper noun in the running world. Track distances follow the same principle: **400m repeats are always "400m repeats"**, never "0.25 mile repeats" or "0.4 km repeats." These conventions are universal across metric and imperial countries.

For display formatting, use context-aware rules: distances below ~2,000m display in meters (track context), distances matching a known race category display as the race name, and all other distances display in the user's preferred unit system. A single `Distance` value object handles all three — the distinction is purely presentational.

---

## Where conversions live: at the boundary, nowhere else

The architecture follows clean architecture principles with conversions at the outermost layer:

```
┌─────────────────────────────────────────────────────┐
│  API / Presentation Layer                            │
│  • Accept input in user's unit, convert TO domain    │
│  • Convert FROM domain to user's unit on output      │
│  • Format "5:30/km" or "8:51/mi" for display         │
├─────────────────────────────────────────────────────┤
│  Application Layer (Services, Use Cases)             │
│  • Works ONLY with Distance, Pace, TimeSpan          │
│  • Never references unit enums or raw doubles         │
├─────────────────────────────────────────────────────┤
│  Domain Layer (Value Objects, Business Rules)        │
│  • Distance (meters), Pace (sec/km), Duration        │
│  • VDOT calculator, pace zone engine, ACWR           │
│  • All math in canonical units                        │
├─────────────────────────────────────────────────────┤
│  Infrastructure / Persistence                        │
│  • Stores raw doubles via EF Core ValueConverters     │
│  • distance_meters DOUBLE, pace_sec_per_km DOUBLE    │
└─────────────────────────────────────────────────────┘
```

**API DTOs must carry explicit units.** Never accept `{ "distance": 5.0 }` — always require `{ "distance": 5.0, "unit": "km" }`. This eliminates an entire class of ambiguity bugs. The controller/mapper converts to the domain type immediately: `"km"` → `Distance.FromKilometers(5.0)`, `"mi"` → `Distance.FromMiles(5.0)`, unknown → validation error.

For EF Core persistence, use `ValueConverter` to map value objects to raw columns:

```csharp
builder.Property(e => e.Distance)
    .HasConversion(d => d.Meters, v => new Distance(v));
```

Weekly mileage and other aggregates should be **computed on the fly** from individual activities. With all distances in meters, the SQL is trivial: `SUM(distance_meters) / 1000.0 AS weekly_km`. This works precisely because canonical storage makes aggregation a simple sum rather than a unit-aware computation. Cache at the application layer for dashboard performance.

---

## The LLM must never do arithmetic: pre-convert everything

Research consistently shows that **even top LLMs achieve only ~88–97% accuracy on basic arithmetic**. Unit conversion — which combines multiplication by irrational-looking constants with time formatting — is firmly in the danger zone. A 2024 study on arxiv explicitly categorizes "Unit Conversion Error" as a distinct LLM failure mode. When ChatGPT users ask for running plans in metric, they routinely get miles unless they explicitly correct it.

The architecture is non-negotiable: **the deterministic computation layer calculates everything in metric, a context assembly layer converts all values to the user's preferred units, and the LLM receives pre-converted data with explicit instructions to never convert.**

The prompt template should state unit preference at least three times — in system instructions, as a data label, and as a reminder after user input:

```
CRITICAL UNIT RULES:
- This athlete uses {imperial|metric} units.
- All distances in {miles|kilometers}. All paces in {min/mile|min/km}.
- NEVER convert units. All data below is already correct.
- Exception: Track intervals (400m, 800m) are ALWAYS in meters.
- Race names (5K, 10K, half marathon, marathon) are proper nouns.
```

**Post-processing validation is your safety net.** Scan LLM output with regex patterns to detect wrong-unit mentions: if a metric user's coaching response contains `min/mile` or `miles` (excluding "mile repeats" as a workout name), flag it. Cross-reference any pace values the LLM mentions against the user's known VDOT zones — if the LLM says "easy pace of 3:30/km" for a 50-minute 10K runner, something is wrong. For structured training plan output, enforce JSON schema validation plus domain rules checking pace plausibility (world record pace ~2:50/km, walking pace ~9:00/km).

Use **low temperature (0.3–0.5)** for any LLM call involving numerical content. The LLM's job is to narrate, explain, and motivate. Every number in its response should originate from the deterministic layer.

---

## User preferences: binary toggle now, granularity later

Every major platform — Strava, Garmin, TrainingPeaks — uses a **binary metric/imperial toggle**. Despite years of user requests for per-sport or per-context customization (triathletes wanting meters for swimming and miles for running), none have implemented it. This tells you something: the binary approach covers 95%+ of users, and the complexity of granular preferences isn't worth it until your user base demands it.

For MVP-0 and MVP-1, implement:

```csharp
public enum UnitPreference { Metric, Imperial }
```

Auto-detect based on locale at signup (`CultureInfo.CurrentCulture` — US, UK, Myanmar, Liberia → Imperial; everywhere else → Metric), but always let the user override. Store this as a user preference that drives all display formatting.

**Handling preference changes mid-plan**: since all data is stored in canonical meters/seconds-per-km, changing the display preference is purely a presentation concern. No data migration needed. Past activities, current plans, and future recommendations all render correctly in the new unit system immediately. This is the single biggest advantage of canonical storage.

---

## Phased implementation: what to build when

### MVP-0 (solo use, metric-only)

Build the foundation that won't require rework:

- **`Distance` value object** storing meters internally with factory methods for meters, kilometers, and miles (include miles now — the cost is one line of code, and retrofitting a raw double is painful)
- **`Pace` value object** storing seconds-per-km with `IsFasterThan`/`IsSlowerThan` semantics
- **`StandardRace` enum** mapping 5K/10K/Half/Marathon to exact distances
- **`PaceRange` value object** for training zones with `Fast`/`Slow` naming
- **EF Core `ValueConverter`** mappings for persistence
- **`UnitPreference` enum** on user profile (default to Metric, but define the type now)
- **Context assembly layer** that formats values for LLM prompts (hardcoded to metric for now, but structured for easy extension)
- **Unit formatting service** interface with a metric-only implementation

### MVP-1 (friends/testers, imperial support)

Activate imperial support without changing the domain:

- **Imperial `UnitFormatter` implementation** producing min:sec/mile and miles
- **Context assembly** reads `UnitPreference` and pre-converts all LLM context data
- **Prompt template** includes unit preference instructions
- **Post-processing regex validator** for LLM output unit correctness
- **API DTOs** require explicit unit fields on all distance/pace inputs
- **Locale-based auto-detection** at signup

### What can wait

- Per-context unit preferences (race names always metric, daily running in preferred unit) — handle via formatting rules, not user settings
- Multi-sport support (swimming in meters, cycling in km, running in miles)
- Original-unit preservation alongside canonical storage (a "display hint" — useful for audit but not critical)
- UnitsNet dependency (your custom value objects are sufficient unless you expand beyond running)

---

## Gotchas and anti-patterns from the field

**The `decimal DistanceKm` trap you're in now.** Your current model stores kilometers as a decimal with the unit baked into the property name. This seems clear until someone adds `DistanceMiles` and another developer sums them. Switch to a `Distance` value object storing meters — the property name `Meters` is the canonical unit, and the type system prevents mixing.

**Storing pace as `TimeSpan`.** `TimeSpan` has tick-level precision (100 nanoseconds) — absurd for running pace. It also doesn't carry the "per what distance" semantic. A `TimeSpan` of 5:30 could be 5:30/km, 5:30/mile, or a 5:30 duration. Use a dedicated `Pace` type.

**Rounding during conversion chains.** The conversion factor **1 mile = 1,609.344 meters** is exact by international definition. But converting meters → miles → kilometers → miles accumulates floating-point error. The rule: **convert only at display boundaries, never round-trip**, and round only at final display. For display precision, use 2 decimal places for distances (8.23 km), mm:ss for paces, and 1 decimal place for weekly volume (45.3 km).

**Letting the LLM see both unit systems.** If your context includes "Easy pace: 5:30/km (8:51/mi)", the LLM may randomly pick either one or mix them within a response. Supply only the user's preferred units.

**Treating "5K" as 5.0 kilometers in display.** A coaching message that says "your 5.00 km race" instead of "your 5K" sounds robotic. Race names are proper nouns. Match distances against known race categories (with a small tolerance for GPS drift) and display the name, not the number.

**Ignoring that all wearable APIs return metric.** Don't build a conversion layer at the data ingest boundary for Strava or Garmin — the data arrives in meters already. Adding an unnecessary conversion step is a bug waiting to happen. For HealthKit, explicitly request meters using `doubleValue(for: .meter())` to normalize to your canonical unit.