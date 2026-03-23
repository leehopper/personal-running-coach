# Research Prompt: Batch 9a — R-019
# Daniels' Running Formula Pace Table Verification and Edition Consistency

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: Verification of Daniels' Running Formula Pace Table Data (VDOT 30-85) and Edition Consistency

Context: I'm building an AI running coach that uses Jack Daniels' VDOT system as the foundation for training pace calculations. The system has two core components:

1. **VdotCalculator** — calculates VDOT from race times using the Daniels/Gilbert oxygen cost equations. Currently references the **3rd edition** of Daniels' Running Formula.
2. **PaceCalculator** — derives training pace zones (Easy, Marathon, Threshold, Interval, Repetition) from VDOT values using a lookup table. Currently references the **4th edition**.

During code review, we found a potential data anomaly in the pace table at the VDOT 49→50 boundary. The step sizes between these two entries are 2-3x larger than all surrounding entries:

```
VDOT 48: EasyMin=316, EasyMax=349, Marathon=278, Threshold=263, Interval=246, Repetition=232
VDOT 49: EasyMin=311, EasyMax=344, Marathon=273, Threshold=259, Interval=242, Repetition=228
VDOT 50: EasyMin=301, EasyMax=331, Marathon=267, Threshold=250, Interval=231, Repetition=216
VDOT 51: EasyMin=297, EasyMax=327, Marathon=263, Threshold=247, Interval=228, Repetition=213
```

Step sizes (seconds per km):
- 48→49: -5, -5, -5, -4, -4, -4 (consistent with all other entries)
- 49→50: -10, -13, -6, -9, -11, -12 (anomalous — 2-3x normal)
- 50→51: -4, -4, -4, -3, -3, -3 (back to normal)

All values are in **seconds per kilometer**. The table covers VDOT 30-85.

What I need to answer:

### 1. VDOT 49→50 Data Verification
- What are the **correct** training paces for VDOT 50 according to Daniels' published tables?
- Is the discontinuity at VDOT 49→50 present in the original published tables, or is it a transcription error?
- If it's a transcription error, what should the correct VDOT 50 values be?
- Check the full table for any other anomalous jumps between adjacent VDOT values across the entire 30-85 range. Flag any entry where the step size deviates significantly from its neighbors.

### 2. Edition Consistency
- The VDOT calculation formulas (oxygen cost and fractional utilization equations) — did these change between the 3rd and 4th editions? Are the Daniels/Gilbert equations the same in both?
- The pace tables — were these updated between the 3rd and 4th editions? If so, what changed and by how much?
- Is it safe to use 3rd edition VDOT formulas with 4th edition pace tables, or does this create mismatches?
- What about the most recent edition — is there a 5th edition, and if so, did anything change?

### 3. Per-Kilometer vs Per-Mile Tables
- Daniels' published tables are traditionally in **per-mile** format. Our code uses **per-kilometer** values. Verify the conversion methodology: are the per-km values simply the per-mile values divided by 1.60934, or does Daniels publish separate per-km tables?
- If the tables were converted from miles, rounding errors could explain anomalies. Check whether the VDOT 50 anomaly disappears when looking at the original per-mile values.

### 4. Alternative Sources for Cross-Referencing
- What online calculators (vdoto2.com, fellrnr.com, etc.) implement the Daniels tables? Do they agree with each other? What VDOT 50 paces do they produce?
- Are there any known errata for Daniels' Running Formula pace tables (any edition)?
- Do other implementations of the Daniels tables (open source, running apps) show the same VDOT 50 values?

Output I need:
- A corrected VDOT 50 entry (or confirmation that the current values are correct), with source citation
- A recommendation on which edition to standardize on for both VDOT formulas AND pace tables
- A list of any other anomalous entries in the VDOT 30-85 range
- Whether the per-mile-to-per-km conversion methodology matters for accuracy
- Any known errata or edition differences that affect our data
