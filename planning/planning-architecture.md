# Planning Architecture

The plan operates across three tiers of granularity. This is both a coaching framework and a technical optimization — the AI never needs to hold an entire year of daily detail in context.

## Macro Layer (Months / Season)

The strategic skeleton of the training cycle. Defines periodization phases: base building, strength/speed development, peak training, taper, race, recovery.

- Changes only when goals shift fundamentally (new race, changed timeline, major injury)
- Stored as a compact summary

## Meso Layer (Weekly Cycles)

The weekly structure within the current phase. Defines which days are long runs, tempo/speed work, easy runs, cross-training, and rest.

- Adjusts moderately based on cumulative fatigue patterns, life events, and phase transitions
- Stored in moderate detail for the current and upcoming weeks

## Micro Layer (Daily Prescriptions)

The specific workout for a given day: distance, pace targets, intervals, effort level, warm-up/cool-down notes.

- Regenerates frequently — this is where most of the AI's reactive work happens
- Only generated a few days ahead at a time

## Context Window Efficiency

This tiered approach directly solves context management. A typical AI call injects:

1. The macro plan as a brief summary (compact)
2. The current meso cycle in detail (moderate)
3. The last few micro prescriptions and logged results (detailed)
4. The user's message or logged data

This keeps token usage efficient while giving the AI everything it needs to make informed decisions.
