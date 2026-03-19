# Safety & Legal Framework

Consolidated safety posture, legal strategy, and compliance requirements for the AI running coach. Informed by R-003 (safety/liability research), R-001 (training methodologies and guardrails), and R-007 (evaluation strategy).

---

## Legal Landscape Summary

The liability environment is more favorable than commonly assumed. No fitness app has ever been successfully sued for coaching advice causing physical injury — all major fitness litigation involves hardware defects or deceptive billing. However, the landscape is shifting: *Garcia v. Character Technologies* (2025) established that AI chatbot output can be treated as a "product" subject to product liability. Section 230 will almost certainly not protect AI coaching products.

### The Architecture Is the Legal Asset

Courts draw a distinction between "information" (protected, like a book) and "functional tools" (subject to product liability, like an aeronautical chart). The hybrid architecture — deterministic computation layer for plan math + LLM for coaching conversation — maps cleanly onto this legal distinction. The deterministic layer is auditable, rule-based, and safety-enforcing. The LLM layer is qualitative, advisory, and non-prescriptive. This separation produces both better coaching and better legal defensibility.

### Key Legal Precedents

- *Garcia v. Character Technologies* (2025): AI chatbot is a "product" — opened strict liability, failure-to-warn, and negligence claims for AI products
- *Winter v. G.P. Putnam's Sons* (9th Cir., 1991): Information in a book is NOT a product; but the court noted software "that fails to yield the result for which it was designed may be another" product
- *Jimenez v. 24 Hour Fitness* (Cal. App. 4th, 2015): Assumption of risk doctrine negated duty of care for inherent exercise risks
- *Meyer v. Uber* (2nd Cir., 2017): Clickwrap agreements enforced at ~70% vs. ~14% for browsewrap
- "Available elsewhere" argument has no established legal defense value and may backfire (*Aetna v. Jeppesen*)

### Assumption of Risk — Strongest Defense

Running is a well-understood activity with inherent risks. Users self-select, control their effort, and can stop at any time. Fitness liability law strongly favors defendants. Exercise waivers are consistently enforced for ordinary negligence (not gross negligence or reckless conduct). Comparative negligence applies when users ignore warnings.

---

## Regulatory Requirements

| Framework | Applies? | Priority | Action |
|-----------|----------|----------|--------|
| FDA | NO — exempt as general wellness (Section 520(o)(1)(B)) | LOW | Keep all language in wellness/fitness domain, never clinical |
| EU AI Act | MINIMAL RISK tier | LOW | Article 50 transparency: disclose users are interacting with AI |
| FTC Section 5 | YES — health claims must be substantiated | HIGH | No unsubstantiated specific claims (e.g., "reduces injury risk by 40%") |
| FTC Health Breach Notification Rule | YES — $43,792/violation/day | HIGH | Breach notification procedures from day one; the app consuming Garmin/health data = "vendor of personal health records" |
| HIPAA | NO — not a covered entity | NONE | Only changes if entering B2B healthcare relationships |
| WA My Health My Data Act | YES — private right of action, no threshold | HIGH | Opt-in consent, deletion rights, privacy policy; covers fitness/wellness data |
| CCPA/CPRA | LIKELY at scale | MEDIUM | Standard California privacy compliance |

### The FTC Surprise

The FTC Health Breach Notification Rule (16 CFR Part 318, amended July 2024) almost certainly applies. An app with "technical capacity to draw identifiable health information from both the user and the fitness tracker is a PHR." "Breach" includes unauthorized disclosures (sharing data with analytics without consent), not just cyberattacks. This is the compliance item most founders miss and the one with the sharpest teeth.

---

## Staged Legal Stack

**Note:** Legal infrastructure is deferred to pre-public release. MVP (personal use / friends) proceeds with accepted risk. Deterministic guardrails are built during MVP for product quality, not legal compliance.

### MVP (founder + friends): $0
- Accept personal risk for this stage
- Document safety layer design decisions and guardrail logic (good practice regardless)
- Deterministic guardrails built for product quality, not legal requirements

### Pre-Public Release: ~$1,500/year
- Form LLC + separate bank account (~$500 filing)
- **Beta Participation Agreement** via clickwrap: confidentiality, "as is" disclaimer, $50 liability cap, warranty disclaimer, assumption of risk, health disclaimer
- **Health screening gate** — PAR-Q-inspired (not formal PAR-Q, which is copyrighted): heart conditions, chest pain, dizziness, bone/joint problems, blood pressure medication, other contraindications. Flags connect to deterministic safety layer — screening without acting on results creates stronger duty of care
- **Privacy policy** — what data is collected, stored, breach notification
- **Comprehensive logging**: screening responses, conversation logs, safety guardrail triggers, consent timestamps

### Public beta (hundreds of users): ~$3,000–4,000/year
- Full Terms of Service: liability cap ($50 or 12-month fees), warranty disclaimer, assumption of risk, **mandatory arbitration with class action waiver**, indemnification, AI disclosure, health disclaimer
- Insurance: Tech E&O (~$800/yr, note: standard E&O excludes bodily injury), General Liability (~$360/yr), Cyber Liability (~$1,775/yr)
- FTC HBNR compliance, WA MHMDA compliance, incident response plan

### Scale (thousands+): ~$15,000–30,000/year
- Enhanced insurance limits ($2M+), AI-specific E&O endorsement, D&O if taking investment
- Professional health-tech legal review of all documents
- Annual compliance audit (FDA claim language, FTC substantiation, state privacy)
- Formal AI governance documentation and output audits

---

## Three Highest-ROI Legal Actions

1. **Mandatory arbitration with class action waiver** — prevents the highest-magnitude liability risk
2. **FTC Health Breach Notification Rule compliance** — real financial teeth, most often missed
3. **Comprehensive logging of deterministic safety layer decisions** — proves guardrails work if ever challenged

---

## Honest Risk Assessment

### Baseline Reality
- ~46% of recreational runners sustain injury annually regardless of coaching method
- Novice: 17.8 injuries per 1,000 hours; experienced: 7.7 per 1,000 hours
- 15–20% of running injuries are stress fractures; 25% of female runners report lifetime stress fracture
- Previous injury doubles risk of new injury (HR = 1.9)
- These numbers apply to every coaching modality — human, AI, book, or none

### Incremental Risk vs. Alternatives
- vs. **self-coaching (~95% of runners)**: clear risk REDUCTION (guardrails prevent worst errors)
- vs. **static plans** (Hal Higdon, Daniels): risk REDUCTION (adaptive guardrails books can't provide)
- vs. **generic ChatGPT**: dramatically SAFER (no load monitoring, no guardrails, hallucinations)
- vs. **human coach**: COMPARABLE with different strengths (more consistent, less physically observant)

### What's Overblown
- Cardiac risk is safety theater — 1 in 50,000 marathon incidence; regular exercise reduces cardiac mortality
- "AI will generate a dangerous plan" — addressed by deterministic safety layer
- "Black box AI" — the LLM doesn't control plan generation or pace math

### What's Underestimated
- **Exercise addiction reinforcement** — ~8.6% of amateur runners meet criteria; technology use correlates with higher injury rates; AI positive reinforcement could exacerbate. Design for this: detect distress at rest days, excessive override requests, compulsive patterns
- **RED-S and nutritional blindspots** — training load monitoring cannot detect energy deficiency; 23–80% of female athletes experience some degree of low energy availability. Address through intake screening (menstrual health, eating habits, bone density history) and pattern detection
- **Scope creep into medical territory** — users will ask about pain, nutrition, body composition. Hard keyword triggers ("chest pain," "persistent pain," "missed periods," "stress fracture") are more reliable than LLM self-policing
- **User misrepresentation** — users who underreport pain or hide conditions undermine all guardrails. Assumption of risk defense is strongest when the product clearly communicated what information it needed

### ACWR Guardrail: Honest Assessment
The ACWR guardrail (0.8–2.0) is defensible but should not be oversold. Associated with injury risk per 2025 meta-analysis, but Impellizzeri et al. (2020) found "no evidence supporting the use of ACWR in training-load-management systems" for injury prevention. The value lies in preventing extreme training errors, not precise injury prediction.

---

## Competitor Legal Practices

Three tiers of sophistication in the market:

**Tier 1 — AI-aware disclaimers** (Strava, Whoop): Standalone AI sections, explicit "use at your own risk," Whoop discloses zero-retention policy with OpenAI. This is the emerging standard.

**Tier 2 — Health disclaimers without AI specifics** (Runna, Peloton, MyFitnessPal): "Not medical advice" sections, assumption of risk, no AI-specific language.

**Tier 3 — Generic boilerplate** (TrainAsONE, Fitbod, smaller startups): Standard "as is" disclaimers only.

Liability caps: Garmin $1, Strava $50 or 12-month fees, Whoop/Noom $100, Peloton 12-month fees. All use mandatory arbitration + class action waivers. No major app requires formal health screening (PAR-Q or equivalent).

---

## Design Implications

These findings feed directly into product design:

1. **Health screening gate must connect to the deterministic layer** — screening users and then ignoring the results creates worse legal exposure than not screening at all
2. **Hard keyword triggers for medical referral** — "chest pain," "persistent pain," "missed periods," "stress fracture" → automatic "see a professional" response, more reliable than LLM judgment
3. **Exercise addiction detection** — monitor rest day distress, excessive override requests, compulsive exercise patterns
4. **RED-S screening at intake** — menstrual health, eating habits, bone density history for at-risk populations
5. **All safety decisions logged** — the event-sourced architecture (DEC-013) provides this naturally; ensure guardrail triggers, screening responses, and consent timestamps are captured
6. **AI disclosure** — EU AI Act Article 50 requires it; also good practice for trust-building
7. **Claim language discipline** — never make specific outcome claims without evidence; "structured training can improve performance" is fine, "reduces injury risk by X%" requires RCT evidence

---

## Population-Specific Safety Framework

*Source: R-011 research. Full artifact: research/artifacts/batch-4b-special-populations-safety.md*

Core principle across all populations: **observe, adapt, and refer — never diagnose, prescribe, or treat.**

### Population-Adjusted Guardrails (DEC-028)

The default safety guardrails are adjusted per population. Key changes from the defaults (single-run spike ≤30%, ACWR 0.8–2.0, ≥70% easy, max 3 quality/week):

- **Pregnant:** ACWR tightened to 0.8–1.3, RPE ceiling 14 (Borg 6–20), ≥85% easy, max 1 quality session (RPE-capped), spike ≤20%, block altitude >1,800m and temp >32°C. Provider clearance required.
- **Postpartum:** Hard block on running before 12 weeks. Walk/run only initially, ≥90% easy, ACWR 0.8–1.3. Monitor for leaking/heaviness/dragging at every check-in.
- **Youth (≤14):** Volume ceilings (15–20 mpw ≤12yo, 25–30 mpw 13–14yo), ≥85% easy, 0–1 quality sessions, ACWR 0.8–1.2, block 7/7 training days under 16. Growth spurt + pain → medical referral.
- **Masters 50+:** 9-day training cycles for 50–59, 4–5 day hard-session spacing for 60+. Volume increase capped at 5–7%/week (50s) or 3–5%/week (60+). ACWR 0.8–1.2 for 60+. Flag no strength training after 3 prompts.
- **Injury return:** 100% easy initially, alternate-day running, traffic-light pain monitoring (green 0–3, amber 4–5, red 6+), two red-light readings = automatic referral and pause.
- **Chronic conditions:** Beta-blocker flag → switch all training from HR-based to RPE. T1D: pre-run safety checklist mandatory. Unmanaged arrhythmia: block vigorous programming until clearance.

### Extended Health Screening (DEC-029)

Additions to the PAR-Q-inspired screening gate (DEC-018):

- **Pregnancy/postpartum:** "Currently pregnant or given birth in last 12 months?" → if yes, require provider clearance (pregnant) or enforce 12-week hard block (postpartum), ask about delivery type and pelvic health assessment
- **Age/development:** Date of birth mandatory. Under-18 triggers parental involvement flow (COPPA consent for <13). Over-50 new to vigorous exercise → recommend medical checkup
- **Female athlete health:** Quarterly check-in on menstrual regularity, energy levels (energy availability proxy), stress fracture history. 3+ missed periods → referral. 2+ career stress fractures → RED-S screening referral
- **Chronic conditions:** Specific prompts for asthma, diabetes (type), arrhythmias, hypertension, thyroid conditions. "Any medications affecting heart rate?" (beta-blocker detection). Unmanaged conditions block vigorous programming
- **Mental health baseline:** Motivation rating (1–5, repeated at check-ins), current mental health challenges, sleep hours — establishes baseline for pattern detection, does not gate access
- **Injury history:** Current pain/injury status. "Told by a professional to avoid running?" — affirmative answer blocks programming until clearance

### Expanded Keyword Triggers (DEC-030)

R-011 significantly expands the DEC-019 keyword trigger system. Major new categories:

- **Pregnancy:** pregnant, trimester, postpartum, C-section, pelvic floor, diastasis, vaginal bleeding, contractions, amniotic fluid, preeclampsia, gestational diabetes
- **Female athlete:** amenorrhea, missed period, irregular period, RED-S, low energy availability, stress fracture (in female runners), ferritin, Female Athlete Triad
- **Youth:** middle school, high school, cross country + school, growth spurt, growing pains, Osgood, Sever's, "my parents/mom/dad"
- **Chronic conditions:** insulin pump, CGM, blood sugar, hypoglycemia, metformin, AFib, pacemaker, ICD, beta blocker, hypothyroidism, Hashimoto, levothyroxine, TSH
- **Injury:** plantar fasciitis, IT band, femoral neck, Achilles + tendon, shin splints, MTSS, patellofemoral, hamstring strain, torn hamstring
- **Mental health/crisis:** suicidal, kill myself, want to die, self-harm, cutting, end it all, eating disorder, anorexia, bulimia, purging, addicted to running, burned out

**Crisis response protocol:** Suicidal ideation triggers immediate crisis resource display (988 Lifeline, Crisis Text Line 741741), cessation of normal coaching, empathetic acknowledgment, and no continued engagement on the crisis topic.

### Return-to-Run Decision Framework

Universal five-stage framework regardless of injury type:

1. **Triage** — coaching-modification territory or immediate referral? (Femoral neck stress fracture = emergency)
2. **Active rest** — zero running, cross-training, monitor pain resolution. Exit: 30-min pain-free walk
3. **Walk/run progression** — traffic-light pain model (green/amber/red) at three checkpoints (during, same day, next morning)
4. **Volume rebuilding** — 10–30% weekly increase (higher % acceptable at very low absolute mileage), all easy pace, target 75–80% pre-injury mileage
5. **Intensity reintroduction** — one quality session/week at reduced intensity, 3–4 weeks to normal distribution

**Iron rule:** "If in doubt, refer out." The coach modifies the plan. Tissue diagnosis, rehab exercise prescription, and medical clearance are not coaching.

### Three-Tier Sensitive Disclosure Escalation

- **Green** (coaching-scope): Normal mood fluctuations, single missed period, minor aches responding to modification → validate, adjust, monitor
- **Amber** (referral recommended): Persistent depression/anxiety, 3+ missed periods, recurring stress fractures, compulsive exercise patterns, OTS symptoms → validate, acknowledge limits, recommend specific professional, continue coaching
- **Red** (immediate action): Suicidal ideation, femoral neck symptoms, syncope during exercise, chest pain, vaginal bleeding during pregnancy, eating disorder disclosure → stop coaching, provide resources, do not continue topic, document

---

*Full research artifacts: research/artifacts/batch-3a-safety-liability.md, research/artifacts/batch-4b-special-populations-safety.md*
