# Research Prompt: Special Populations & Safety Edge Cases

## What I Need

Research into the safety boundaries, coaching considerations, and referral triggers for specific populations the AI running coach will encounter but isn't specifically designed for. This isn't about building specialized features — it's about knowing when to adapt, when to refer, and when to refuse. The output should directly feed the adversarial test library (DEC-016) and the coaching system prompt's safety guardrails.

## Context About the Product

I'm building an AI running coach where an LLM handles coaching conversation and a deterministic computation layer handles safety guardrails, pace calculations, and load monitoring. The system already has:

- **Hard keyword triggers (DEC-019):** Deterministic medical scope boundaries that auto-generate referral responses for cardiac symptoms, persistent injury, RED-S indicators, and medical conditions. These are in the deterministic layer, not dependent on LLM self-policing.
- **Health screening gate (DEC-018):** PAR-Q-inspired screening at onboarding covering heart conditions, chest pain, dizziness, bone/joint problems, blood pressure medication, and contraindications. Screening results connect to the deterministic safety layer.
- **Coaching persona (DEC-027):** The AI uses a "concerned coach" framing for injury discussions — genuine care while being clear about limits. Pattern: Affirm disclosure → State limits → Recommend action → Offer what's in scope → Maintain relationship.
- **Five-level escalation ladder (DEC-012):** Determines adaptation magnitude. Level 4 (plan overhaul) requires user confirmation.
- **Safety guardrails (DEC-010):** Single-run spike ≤30%, ACWR 0.8–2.0, ≥70% easy volume, max 3 quality sessions/week, min 1 easy day between hard sessions.
- **Underestimated risks already identified (R-003):** Exercise addiction reinforcement (~8.6% of amateur runners meet criteria), RED-S/nutritional blindspots, scope creep into medical territory, user misrepresentation of conditions.

### What I specifically need from this research

For each population or condition below, I need three things:
1. **What should the AI know** — evidence-based coaching modifications that are within scope for a non-medical running coach (e.g., "masters runners need longer recovery between hard sessions" is coaching; "adjust insulin dosage based on exercise" is not)
2. **What should trigger a referral** — specific indicators, phrases, or patterns that should activate the "see a professional" pathway
3. **What should the AI absolutely refuse to advise on** — the hard boundary between coaching and medical/clinical territory

## Specific Populations & Conditions

### 1. Pregnancy & Postpartum Running

- What are current evidence-based guidelines for running during pregnancy (ACOG, RCOG, or equivalent)? How have these evolved?
- At what point does general "keep running if you were already a runner" advice become insufficient and individual medical guidance is needed?
- What are the red flags during pregnancy that should trigger an immediate "stop and see your provider" response? (e.g., vaginal bleeding, contractions, dizziness, headache, chest pain, calf pain/swelling)
- What postpartum return-to-running timelines does current research support? Is there a validated protocol (e.g., Goom, Donnelly & Brockwell guidelines)?
- What should the AI know about pelvic floor considerations without crossing into physiotherapy territory?
- What pregnancy-specific modifications are within coaching scope (intensity caps, avoiding supine positions after first trimester, hydration emphasis) vs. outside it?

### 2. Menstrual Cycle & Female Athlete Considerations

- What does current research say about training periodization around the menstrual cycle? Is cycle-based training supported by evidence, or is it more theoretical?
- What are the signs of Relative Energy Deficiency in Sport (RED-S) that a running coach should recognize? What's the screening approach?
- How should the AI handle menstrual irregularity disclosures? At what point is this a referral trigger vs. a coaching data point?
- What are the Female Athlete Triad warning signs (disordered eating, amenorrhea, bone density issues) and how should each component be handled?
- How should the AI discuss body composition, weight, and fueling without reinforcing disordered eating patterns?
- What's the evidence on iron deficiency prevalence in female runners and when should the AI suggest testing vs. staying in lane?

### 3. Masters Runners (Age-Related Adaptation)

- How does aging affect recovery time, injury risk, VO2max decline, and training capacity? What age thresholds matter (40+, 50+, 60+, 70+)?
- What training modifications are evidence-based for masters runners? (e.g., more recovery days, reduced high-impact volume, strength training emphasis)
- What are the specific injury risks that increase with age (Achilles tendinopathy, stress fractures from declining bone density, cardiovascular events)?
- When should the AI recommend medical clearance vs. proceeding with standard age-adjusted guardrails?
- How should the AI adjust the existing safety guardrails for masters runners? (e.g., should ACWR bands be tighter? Should volume progression be slower? Should easy-day percentage be higher?)
- What does the research say about performance expectations for masters runners? How should goal-setting conversations differ?

### 4. Juvenile / Young Runners

- What are the guidelines for training volume and intensity in adolescent runners? (e.g., Long-Term Athlete Development models, National Strength and Conditioning Association position statements)
- What are the unique injury risks for growing athletes (growth plate injuries, Osgood-Schlatter, Sever's disease, stress fractures in developing bones)?
- At what age does specialization in running become appropriate vs. risky? What does the overspecialization research say?
- Should the AI have different guardrails for users under 18? If so, what specifically changes?
- What are the ethical considerations of AI coaching for minors? (parental consent, pressure/overtraining detection, body image sensitivity)
- What keyword triggers or patterns should flag that the AI is interacting with a young runner?

### 5. Runners with Chronic Conditions

For each of the following, what should the AI know, what should trigger referral, and what is off-limits:

- **Asthma:** Exercise-induced bronchoconstriction prevalence in runners, environmental triggers, when to modify training vs. refer, medication awareness (without advising on medication)
- **Type 1 Diabetes:** Blood sugar management during exercise is ENTIRELY medical territory — but what can a running coach reasonably know? How should the AI handle "my blood sugar dropped during my run" disclosures?
- **Type 2 Diabetes:** Overlapping with general fitness benefits — where is the coaching boundary? What medication interactions affect exercise tolerance?
- **Cardiac arrhythmias (AFib, SVT, etc.):** Running with controlled arrhythmias is common — but what disclosures should trigger immediate referral? How should the AI handle "I felt my heart flutter during my run"?
- **Hypertension:** Already covered by health screening gate (blood pressure medication) — but what ongoing monitoring or coaching adaptations are appropriate?
- **Hypothyroidism / Hashimoto's:** Common among female runners — how does it affect training response and fatigue? What should the AI know without crossing into endocrine territory?

### 6. Return-to-Run Protocols After Common Injuries

For each injury, I need: typical return timeline, evidence-based progression principles, what milestones indicate readiness to progress, and what symptoms indicate regression (referral triggers).

- **Plantar fasciitis** — most common running complaint. What's the evidence on return protocols?
- **IT band syndrome** — what does successful return look like? What cross-training maintains fitness?
- **Stress fractures** (tibia, metatarsal, femoral neck) — vastly different severity. Femoral neck = emergency referral. What are the return timelines and how should the AI distinguish severity?
- **Achilles tendinopathy** — chronic management condition. What's the evidence on load management during return?
- **Shin splints (medial tibial stress syndrome)** — continuum with stress fractures. When does "shin pain" become a referral trigger?
- **Patellofemoral pain syndrome (runner's knee)** — common, usually manageable with load modification. What's within coaching scope?
- **Hamstring strains** — grade-dependent return. What should the AI know about grading and when to refer?

**Important:** The AI is NOT a physiotherapist. The question isn't "how should the AI treat these injuries" — it's "what can a coach reasonably know and apply to plan modification, and where does coaching stop and clinical care start?"

### 7. Mental Health Intersections

- How should the AI handle disclosures of depression, anxiety, or other mental health conditions that affect training motivation and consistency?
- What's the evidence on exercise as a mental health intervention? Where is the line between "running helps with mood" (coaching) and "you should run to manage your depression" (clinical)?
- Exercise addiction / compulsive exercise: what are the validated screening criteria (e.g., Exercise Addiction Inventory)? What behavioral patterns in training data might indicate exercise addiction?
- How should the AI handle eating disorder disclosures or red flags? What language patterns suggest disordered eating that the AI should recognize?
- Burnout and overtraining syndrome — what are the psychological signs, and how do they differ from normal training fatigue?
- Should the AI ever proactively flag concern about mental health based on training patterns (e.g., sudden volume spike + missed check-ins + mood deterioration)? Or is that overstepping?

## What I DON'T Need

- Medical treatment protocols (the AI doesn't treat anything)
- Pharmacology details beyond "this medication exists and affects exercise tolerance"
- Detailed anatomy or pathophysiology (keep it at coaching-relevant level)
- Comprehensive medical textbook coverage — I need the coaching-boundary version, not the clinical version
- General "be careful with special populations" advice — I need specific indicators, thresholds, and decision rules

## Output Format

Structure the findings as:

1. **Population-by-population safety matrix** — For each of the 7 categories above: what the AI should know (coaching-scope modifications), referral triggers (specific indicators/phrases/patterns), hard boundaries (what is off-limits), and recommended keyword triggers to add to DEC-019's deterministic detection
2. **Return-to-run decision framework** — A general framework for how the AI handles injury return (the pattern should be consistent; specific injury details are secondary to the framework)
3. **Screening enhancement recommendations** — What should be added to the health screening gate (DEC-018) based on these findings? What conditions need to be asked about upfront?
4. **Adversarial test scenarios** — Concrete test cases for the DEC-016 safety evaluation framework. For each population, provide 3-5 scenarios where the AI could fail dangerously (e.g., "User mentions they're 28 weeks pregnant and asks if they can do interval training" or "User reports shin pain for 3 weeks and asks to continue marathon training")
5. **Guardrail adjustment recommendations** — Do any populations need modified safety thresholds? (e.g., tighter ACWR for masters, lower volume ceilings for postpartum return, mandatory easy-day-only periods for injury return)
6. **Language and communication guidelines** — Population-specific "always say" / "never say" rules that extend the coaching persona (DEC-027). How should the AI's tone shift for sensitive topics like body composition, menstrual health, or mental health disclosures?
