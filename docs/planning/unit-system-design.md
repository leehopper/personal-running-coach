# Unit System Design

How RunCoach handles metric, imperial, and mixed-unit contexts. Based on R-020 research (March 2026).

## Design Principles

1. **Canonical internal storage in meters and seconds-per-km** — industry standard (Strava, Garmin, TrainingPeaks all do this)
2. **Convert only at display boundaries** — API layer, context assembly for LLM, UI formatting
3. **The LLM never does unit math** — all values pre-converted before prompt injection
4. **Race distances are proper nouns** — "5K" not "5.00 km" or "3.11 mi"
5. **Track intervals are always meters** — "400m repeats" regardless of user preference

## Type System

### Value Objects (readonly record structs)

**`Distance`** — stores meters internally, exposes `.Kilometers` and `.Miles` computed properties. Factory methods: `FromMeters()`, `FromKilometers()`, `FromMiles()`.

**`Pace`** — stores seconds-per-km internally. Exposes `.SecondsPerMile`, `.KilometersPerHour`. Uses `IsFasterThan()`/`IsSlowerThan()` methods instead of comparison operators (faster pace = lower number is counterintuitive for operators).

**`PaceRange`** — uses `Fast`/`Slow` naming instead of `Min`/`Max` to avoid the faster=smaller confusion.

**`StandardRace`** — enum mapping 5K/10K/Half/Marathon to exact meter distances. Display as proper nouns.

### Why These Choices

- **`double` not `decimal`** — GPS has ±3-10m inherent imprecision. `double` gives ~15 digits of precision (nanometer-level for marathon distances) and performs better for VDOT math.
- **`readonly record struct`** — immutable, value semantics (two distances from different units that resolve to the same meters are equal), stack-allocated (zero GC pressure).
- **No comparison operators on Pace** — `IsFasterThan()`/`IsSlowerThan()` make intent unambiguous.

## Current State (POC 1)

The current codebase uses raw types with unit-in-name conventions:
- `decimal DistanceKm` — distances as raw decimals
- `TimeSpan AveragePacePerKm` — pace as TimeSpan (no "per what distance" semantics)
- `PaceRange(MinPerKm, MaxPerKm)` — confusing naming (faster pace = Min)
- `string PreferredUnits` — passed through to LLM prompt, no backend logic
- PaceCalculator table in seconds-per-km (correct canonical unit, wrong container type)
- VdotCalculator race distances in meters via `FrozenDictionary<string, double>` (already correct)

This is documented as POC cleanup debt. The value object migration is a prerequisite for MVP-0.

## Architecture Layers

```
API / Presentation Layer
  Accept input in user's unit, convert TO domain types
  Convert FROM domain to user's unit on output
  Format "5:30/km" or "8:51/mi" for display

Application Layer (Services)
  Works ONLY with Distance, Pace, TimeSpan
  Never references unit enums or raw doubles

Domain Layer (Value Objects, Business Rules)
  Distance (meters), Pace (sec/km), Duration
  VDOT calculator, pace zone engine, ACWR
  All math in canonical units

Infrastructure / Persistence
  EF Core ValueConverters: Distance → distance_meters DOUBLE
  Pace → pace_sec_per_km DOUBLE
```

## LLM Integration

The context assembly layer (ContextAssembler) handles unit conversion for LLM prompts:
- Read user's `UnitPreference` (Metric or Imperial)
- Convert all Distance and Pace values to preferred display format
- Include explicit unit rules in the prompt:
  ```
  CRITICAL UNIT RULES:
  - This athlete uses {imperial|metric} units.
  - All distances in {miles|kilometers}. All paces in {min/mile|min/km}.
  - NEVER convert units. All data below is already correct.
  - Exception: Track intervals (400m, 800m) are ALWAYS in meters.
  - Race names (5K, 10K, half marathon, marathon) are proper nouns.
  ```
- Post-processing validation: regex scan for wrong-unit mentions in LLM output

## Wearable Data Ingestion

All major wearable APIs return metric:
- **Strava API**: meters and m/s
- **Garmin Health API**: `distanceInMeters`, `averageSpeedInMetersPerSecond`
- **FIT files**: meters with centimeter precision
- **Apple HealthKit**: `HKQuantity` objects — request `.meter()` explicitly

No conversion needed at the ingest boundary for Strava or Garmin. For HealthKit, explicitly request meters.

## Phased Implementation

### MVP-0 (metric-only, but foundation in place)
- `Distance`, `Pace`, `PaceRange`, `StandardRace` value objects
- `UnitPreference` enum on user profile (default Metric)
- EF Core `ValueConverter` mappings
- Context assembly hardcoded to metric but structured for extension
- Unit formatting service interface with metric-only implementation

### MVP-1 (imperial support activated)
- Imperial `UnitFormatter` implementation
- Context assembly reads `UnitPreference`, pre-converts LLM data
- Prompt template includes unit preference instructions
- Post-processing regex validator for LLM unit correctness
- API DTOs require explicit unit fields: `{ "distance": 5.0, "unit": "km" }`
- Locale-based auto-detection at signup

### Deferred
- Per-context unit preferences (handle via formatting rules, not user settings)
- Multi-sport support
- Original-unit preservation alongside canonical storage
- UnitsNet dependency (custom value objects sufficient for running-only)

## Anti-Patterns to Avoid

- **Unit-in-property-name** (`DistanceKm`) — seems clear until someone adds `DistanceMiles` and sums them
- **`TimeSpan` for pace** — no "per what distance" semantics, tick-level precision is absurd for running
- **Round-trip conversions** — meters → miles → km → miles accumulates error. Convert only at display, never round-trip
- **Showing both units to the LLM** — "5:30/km (8:51/mi)" causes random unit mixing in responses
- **Treating "5K" as "5.00 km"** — race names are proper nouns in running culture

## References

- R-020 research artifact: `docs/research/artifacts/batch-9b-unit-system-design.md`
- Strava API: all distances in meters, speeds in m/s
- Garmin Health API: self-documenting metric field names
- 1 mile = 1,609.344 meters (exact by international definition)
