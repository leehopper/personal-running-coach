# Legal and safety landscape for an AI running coach

**An AI running coach with deterministic safety guardrails faces a more favorable liability environment than most founders assume.** No fitness app has ever been successfully sued for bad training advice causing physical injury — all major fitness litigation involves hardware defects or deceptive billing. The product's architecture (LLM for conversation, deterministic layer for plan math) maps cleanly onto a legal distinction courts already draw between "information" (protected) and "functional tools" (product liability). The realistic risk surface is narrower than the generic fear suggests, concentrated in areas every coaching modality struggles with: undiagnosed conditions, user dishonesty, and nutritional/biomechanical blindspots the system can't measure. The regulatory picture is clean — the FDA exempts general wellness software, HIPAA doesn't apply, and the EU AI Act classifies this as minimal risk. The FTC's Health Breach Notification Rule is the compliance item most founders overlook and the one that carries real teeth.

---

## 1. No one has been sued for bad training advice — yet

Despite millions of users across Peloton, Strava, Noom, Garmin Coach, and dozens of other platforms, **zero lawsuits have been filed against fitness apps for coaching advice causing physical injury**. Every major fitness technology lawsuit falls into one of four categories: physical product defects (Peloton's Tread+ recall, **$19 million CPSC penalty**), deceptive subscription practices (Noom's **$56 million class action settlement**), misrepresenting AI as human coaching (Noom's "personalized coach" complaint), or IP disputes. The advice itself — workout plans, intensity recommendations, recovery guidance — has never been the basis for a successful claim.

This doesn't mean liability is impossible. The legal landscape shifted meaningfully in May 2025 when Judge Anne Conway ruled in *Garcia v. Character Technologies* that **an AI chatbot is a "product" subject to product liability law**, not merely a service. The court rejected First Amendment protection for LLM outputs, stating defendants "fail to articulate why words strung together by an LLM are speech." This case, involving a teen's suicide after Character.AI interactions, allowed strict product liability, failure-to-warn, and negligence claims to proceed. Character.AI and Google settled multiple related lawsuits in January 2026. Meanwhile, OpenAI faced seven California lawsuits by late 2025, four involving wrongful death.

**Section 230 will almost certainly not protect an AI coaching product.** The emerging consensus across the Congressional Research Service, Harvard Law Review, the ABA, and the Center for Democracy and Technology is that generative AI occupies the role of content creator, not content host. The *Lemmon v. Snap* (9th Cir.) and *Anderson v. TikTok* (3rd Cir.) precedents already allow product liability claims to survive Section 230 for platform design choices. A bipartisan Senate bill (the "No Section 230 Immunity for AI Act") would codify this explicitly.

The critical legal framework for this product comes from **the spectrum between books and aeronautical charts**. In *Winter v. G.P. Putnam's Sons* (9th Cir., 1991), information in a book — even dangerously wrong mushroom identification that led to liver transplants — was held NOT to be a "product." But in *Brocklesby v. United States* and *Aetna Casualty v. Jeppesen*, aeronautical charts were treated as products because they function as technical tools for direct operational use. The court in *Winter* noted in dicta that "computer software that fails to yield the result for which it was designed may be another" product. This creates a spectrum directly relevant to the product's architecture: the deterministic computation layer (pace calculations, load monitoring, guardrail enforcement) resembles a functional tool; the LLM coaching conversation (explanation, motivation, adaptation reasoning) resembles a book's expression. The *Garcia* ruling reinforced this split, distinguishing between claims arising from "defects in the app" (product liability applies) and "ideas or expressions within the app" (no product liability). **The hybrid architecture is legally advantageous precisely because it separates the product-like computation from the expression-like conversation.**

### The "available elsewhere" argument doesn't help

The argument that "users can get equivalent advice from ChatGPT" has **no established legal defense value** and may actually backfire. The *Aetna v. Jeppesen* line of cases shows that repackaging freely available government aviation data into a chart format *created* product liability, even though the underlying data was accurate and publicly available. Courts focus on reliance: a personalized AI running coach implies the user should trust and follow its output, which increases duty of care compared to a generic chatbot query. Packaging information as a purpose-built product signals authority and invites reliance.

### Assumption of risk is the strongest defense

Fitness liability law strongly favors defendants. In *Jimenez v. 24 Hour Fitness* (Cal. App. 4th, 2015), the primary assumption of risk doctrine negated the defendant's duty of care entirely for inherent exercise risks. In *Rostai v. Neste Enterprise* (Cal. App. 4th, 2006), the court held that "a mere difference of opinion as to how student should be instructed does not constitute evidence of gross negligence" — directly applicable to coaching methodology disputes. Exercise waivers are consistently enforced for ordinary negligence across jurisdictions, though they do not cover gross negligence or reckless conduct. Running is a well-understood activity with inherent risks users are presumed to know. Courts apply comparative negligence when users ignore warnings, proportionally reducing damages.

The learned intermediary doctrine (from pharmaceutical law) does not directly apply — there's no intermediary between the AI and the user. But the underlying principle that user autonomy breaks the liability chain has force: users self-select into running, choose when to run, control their own effort, and can stop at any time.

---

## 2. Regulatory map: mostly clear, with one surprise

| Framework | Applies? | Key citation | Priority |
|---|---|---|---|
| **FDA** | **NO** — exempt as general wellness | Section 520(o)(1)(B) FD&C Act; Jan 2026 General Wellness Guidance | LOW |
| **EU AI Act** | **MINIMAL RISK** tier | Regulation (EU) 2024/1689, Art. 6, Annex III | LOW |
| **FTC Section 5** | **YES** — health claims must be substantiated | 15 U.S.C. § 45; Health Products Compliance Guidance (Dec 2022) | HIGH |
| **FTC Health Breach Notification Rule** | **YES** — this is the surprise | 16 CFR Part 318 (Final Rule effective July 29, 2024) | HIGH |
| **HIPAA** | **NO** | 45 CFR Parts 160, 164 | NONE |
| **WA My Health My Data Act** | **YES** — private right of action | RCW 19.373 | HIGH |
| **California CCPA/CPRA** | **LIKELY** at scale | Cal. Civ. Code § 1798.100 et seq. | MEDIUM |
| **Illinois BIPA** | **NO** — HR/fitness data not biometric identifiers | 740 ILCS 14/ | NONE |

**FDA: Clearly exempt.** Under Section 520(o)(1)(B) of the FD&C Act, software intended "for maintaining or encouraging a healthy lifestyle and unrelated to the diagnosis, cure, mitigation, prevention, or treatment of a disease or condition" is not a device. The January 2026 guidance explicitly expanded this to include products that estimate physiologic parameters (including heart rate) via non-invasive sensing for wellness purposes. The key is claims, not technology — heart rate data, adaptive algorithms, and AI components don't trigger regulation. What triggers it: referencing specific diseases, using clinical thresholds, characterizing outputs as "abnormal" or diagnostic, claiming clinical equivalence with a medical device. Keep all language in the wellness/fitness domain ("training load," "recovery status," "fitness improvement") and never the clinical domain ("overtraining syndrome diagnosis," "cardiac risk assessment").

**EU AI Act: Minimal risk with one transparency obligation.** The product doesn't appear in any Annex III high-risk category (biometrics, critical infrastructure, education, employment, essential services, law enforcement, migration, justice). It falls in the minimal-risk tier alongside video games and spam filters — no mandatory obligations. However, **Article 50 transparency requirements** apply to any AI chatbot: users must be informed they're interacting with AI. This is a simple disclosure obligation.

**FTC: The real regulatory exposure.** Two distinct requirements apply. First, Section 5 prohibits deceptive claims — the FTC's 2022 Health Products Compliance Guidance requires "competent and reliable scientific evidence" for health claims, generally meaning randomized controlled trials. General claims ("structured training can improve running performance") need less substantiation than specific claims ("our AI reduces injury risk by 40%"). The FTC's September 2024 "Operation AI Comply" enforcement sweep signals escalating scrutiny of AI product claims. Second, and more critically, **the FTC Health Breach Notification Rule (16 CFR Part 318, amended July 2024) almost certainly applies.** The FTC explicitly stated that a fitness app with "technical capacity to draw identifiable health information from both the user and the fitness tracker is a PHR, even if some users elect not to connect the fitness tracker." An app consuming data from Garmin/Strava/Apple Health APIs plus user-inputted fatigue/soreness reports is a "vendor of personal health records." Penalties reach **$43,792 per violation per day** for failure to notify of breaches. "Breach" includes unauthorized disclosures (sharing data with analytics without consent), not just cyberattacks.

**HIPAA: Does not apply.** The product is not a covered entity or business associate. Consumer fitness data (pace, HR, mileage, subjective reports) collected directly by an app is "consumer health information," not Protected Health Information, because it's not created or received by a covered entity. Integrating with Apple Health or Garmin APIs doesn't change this — those are consumer technology platforms, not HIPAA entities. This changes only if the product enters B2B healthcare relationships (prescribed by providers or contracted by health plans).

**Washington's My Health My Data Act is the sleeper concern.** Effective March 2024 with no revenue or user-count thresholds, it covers any entity targeting Washington consumers that collects "consumer health data" — defined to explicitly include fitness data, wellness data, and physical activity data. It requires opt-in consent for collection, separate consent for sharing, signed written authorization for selling, and consumer rights to access and delete. Crucially, it includes a **private right of action** — consumers can sue directly.

---

## 3. What competitors actually do (and don't do)

The industry operates with remarkably thin legal infrastructure. **No major fitness app requires formal health screening (PAR-Q or equivalent) before allowing users to follow training plans.** Most rely on boilerplate disclaimers and "as is" warranties.

### Three tiers of legal sophistication

**Tier 1 — AI-aware disclaimers (Strava, Whoop).** Strava's January 2026 terms include a standalone AI section: "AI technologies have known and unknown risks and limitations and may make mistakes; you understand and agree that you use AI Features at your own risk." Whoop's terms explicitly name AI Technology including "third party large language models" and state: "To the extent permitted by law, WHOOP bears no liability to you or anyone else arising from or relating to your use of AI Technology." Whoop also discloses a "Zero-Retention/Zero Training Policy" with OpenAI for its GPT-4 powered coach. These represent the emerging standard for AI fitness features.

**Tier 2 — Explicit health disclaimers without AI specifics (Runna, Peloton, MyFitnessPal).** Runna devotes an entire ToS section to "Not Medical or Professional Advice," stating the app "is not intended to be a substitute for professional medical advice, diagnosis or treatment" and "does not create a doctor-patient or other professional healthcare relationship." Peloton labels its service as providing information "for educational and entertainment purposes only." MyFitnessPal includes eating disorder safeguards and an explicit assumption of risk clause for physical activity. None of these specifically address their algorithms or AI components.

**Tier 3 — Generic boilerplate only (TrainAsONE, Fitbod, smaller startups).** Standard "as is" disclaimers without health-specific or AI-specific language. Fitbod does include a notable provision for its "Ask a Trainer" feature: "By the nature of the generalized question and response format, Fitbod and its trainers cannot possibly ascertain what activities are safe for you and your body."

### Liability caps reveal the industry's risk assessment

Liability caps range wildly: **Garmin caps at $1.00** (the lowest in the industry), Strava at **$50 or 12-month fees** (whichever is greater), Whoop and Noom at **$100**, and Peloton at **12-month subscription fees**. Runna broadly disclaims all liability without a specific dollar cap. All US-based companies use mandatory binding arbitration with class action waivers.

### The Runna injury controversy is instructive

Runna (acquired by Strava in April 2025 for a reported multi-million-pound deal) has faced social media criticism from physiotherapists and users reporting stress fractures, shin splints, and muscle tears attributed to plans "ramping up both distance and intensity too quickly." TikTok and Reddit saw significant complaint volumes. However, **no peer-reviewed study has established higher injury rates** for Runna users vs. traditional plans, and **no formal lawsuits or regulatory actions** have resulted. The controversy is reputational, not legal — but it demonstrates that user-generated injury narratives can damage an AI training product regardless of whether the claims are epidemiologically valid.

---

## 4. Practical legal requirements staged by product maturity

### MVP-0 (founder only): ~$500

Form an LLC before spending money on hosting or APIs — it's cheap liability protection ($50–$500 filing fee depending on state). Open a separate business bank account immediately; commingling funds is the most common way founders inadvertently pierce the corporate veil. Begin documenting safety layer design decisions and guardrail logic. Draft disclaimer and ToS language. No insurance needed.

### MVP-1 (10–50 testers): ~$1,000/year

The minimum viable legal stack has four components. First, a **Beta Participation Agreement** combining confidentiality, "as is" disclaimer ("TESTER ACKNOWLEDGES THAT BETA PRODUCTS ARE EXPERIMENTAL IN NATURE"), liability cap at $50 aggregate, full warranty disclaimer, assumption of risk for physical activity, and health disclaimer — all via clickwrap (checkbox + "I Agree" button). Courts enforce clickwrap agreements at approximately **70% compared to ~14% for browsewrap** (*Meyer v. Uber*, 2nd Cir. 2017 upheld clickwrap; *Specht v. Netscape* rejected browsewrap). Second, a **simplified health screening gate** — not a formal PAR-Q (which is copyrighted and requires written consent from the PAR-Q+ Collaboration for electronic use) but PAR-Q-inspired questions covering heart conditions, chest pain during activity, dizziness, bone/joint problems, blood pressure medication, and other known contraindications. If any flag triggers, display a physician consultation recommendation and log the response. Third, a **privacy policy** covering what data is collected, how it's stored, and breach notification procedures. Fourth, **comprehensive logging** from day one: health screening responses, LLM conversation logs, safety guardrail triggers, consent timestamps.

One critical nuance on health screening: **implementing screening creates a stronger duty of care.** If the app screens users and then ignores the results — providing high-intensity plans to someone who flagged cardiovascular issues — the legal position is worse than not screening at all. The screening must connect to the deterministic safety layer, adjusting parameters for flagged users.

### Public beta (hundreds of users): ~$3,000–4,000/year

Full Terms of Service with all critical provisions: limitation of liability (cap at lesser of $50 or 12-month fees), warranty disclaimer, assumption of risk, **mandatory arbitration with class action waiver**, indemnification, AI disclosure, health disclaimer, governing law. The arbitration clause is arguably the single most protective provision — it prevents class actions, which represent the highest-magnitude liability risk.

Insurance becomes necessary at this stage. The recommended stack:

- **Tech E&O (Errors & Omissions)**: ~$800/year for $1M/$1M limits — covers claims that AI gave bad advice leading to injury. Critical gap: standard Tech E&O typically **excludes bodily injury**, per K&L Gates analysis.
- **General Liability**: ~$360/year — covers bodily injury claims the E&O won't.
- **Cyber Liability**: ~$1,775/year — covers data breaches, required given FTC HBNR compliance. Bundling with E&O saves 16–25%.

Important: emerging AI-specific E&O endorsements (Embroker, Armilla/Lloyd's) exist but may carry sublimits as low as **$25,000 within a general $5M E&O policy**. Confirm AI coverage explicitly with the insurer.

Also at this stage: FTC Health Breach Notification Rule compliance (notification procedures for any breach of unsecured health information), Washington MHMDA compliance (privacy policy, opt-in consent, deletion rights), and an incident response plan for user-reported injuries.

### Scale (thousands+ users): ~$15,000–30,000/year

Enhanced insurance limits ($2M+ aggregate), AI-specific E&O endorsement, D&O insurance if taking investment ($9,000–$30,000/year). Professional legal review of all documents by health tech specialty counsel. Annual compliance audit covering FDA claim language, FTC substantiation, state privacy laws. Consider entity conversion from LLC to Delaware C-Corp if seeking VC. Implement formal AI governance documentation and regular output audits.

---

## 5. Honest risk matrix — what actually matters

### The baseline no one can escape

**Approximately 46% of recreational runners sustain a running-related injury every year.** Novice runners face **17.8 injuries per 1,000 hours of running** — more than double the rate of experienced runners (**7.7 per 1,000 hours**). Stress fractures represent 15–20% of all running injuries. **25% of female runners** report lifetime stress fracture history. Previous injury doubles the risk of new injury (HR = 1.9). These numbers apply regardless of coaching method — human, AI, book, or none. Any liability analysis must start from this baseline.

### ACWR is a reasonable guardrail, not a validated prevention tool

The product's ACWR guardrail (0.8–2.0) is defensible but should not be oversold. A 2025 meta-analysis of 22 cohort studies found ACWR associated with injury risk, supporting the general principle. However, Impellizzeri et al. (2020) concluded there is "no evidence supporting the use of ACWR in training-load-management systems" for injury prevention. The "sweet spot" has not been consistently replicated; a 10-month RCT found no injury reduction with ACWR-based load management; most evidence comes from team sports, not individual endurance sports. The value of the deterministic layer lies in **preventing extreme training errors** (doubling mileage in a week, all-hard training) rather than precise injury prediction. This is still a significant advantage over alternatives with no guardrails.

### Scenario-by-scenario risk assessment

| Scenario | Likelihood | Severity | vs. Alternatives |
|---|---|---|---|
| Overuse injury (general) | **HIGH** — baseline ~46%/year | Moderate | **LOWER** than self-coaching or static plans (guardrails prevent worst errors) |
| Stress fracture | **MODERATE** — 15–20% of injuries | Moderate-High | **SLIGHTLY LOWER** (load monitoring helps; intrinsic factors like BMD, nutrition can't be addressed by any coach) |
| Sudden cardiac death | **VERY LOW** — 0.002% marathon incidence | Catastrophic | **NEUTRAL** — identical across all modalities; regular structured exercise is actually net protective (5× lower relative risk for habitually active vs. sedentary) |
| Overtraining syndrome | **LOW-MODERATE** — 7–21% annual in endurance athletes | Moderate | **LOWER** (mandatory recovery, load monitoring) |
| RED-S exacerbation | **MODERATE** — affects 23–80% of female athletes to varying degrees | High | **NEUTRAL to SLIGHTLY HIGHER** (enables more systematic training without nutritional awareness) |
| Exercise addiction reinforcement | **LOW-MODERATE** — ~8.6% of amateur runners at risk | Moderate | **SLIGHTLY HIGHER** (technology engagement is itself a risk factor; positive reinforcement loop) |
| Delayed medical care from AI reassurance | **LOW** | Moderate-High | **UNIQUE TO AI** — mitigable by design |
| Lawsuit/regulatory action | **LOW** (no precedent for coaching advice claims) | High | **COMPARABLE** to human coaches; lower than hardware products |

### What's overblown

**Cardiac risk is safety theater territory.** Sudden cardiac death during marathon running occurs at 1 in 50,000. Regular exercise *reduces* overall cardiac mortality. An AI coach that builds fitness gradually may actually reduce risk compared to sporadic unstructured exercise. A "consult your physician" disclaimer fully addresses this — the same standard used by every running book, magazine training plan, and coaching service ever published.

**"The AI will generate a dangerous plan" fears are largely addressed by the architecture.** With hard guardrails on volume spikes, intensity distribution, and recovery, the deterministic layer prevents the training errors most likely to cause harm. A static Hal Higdon plan has zero adaptive guardrails and has safely served millions of runners for decades. This product is strictly more protective.

**"Black box AI" concerns don't apply here.** The LLM doesn't control plan generation or pace math. The deterministic layer is transparent, auditable, and rule-based. This is architecturally different from a pure LLM generating workout prescriptions, and the distinction matters legally.

### What's underestimated

**Exercise addiction reinforcement is the most underappreciated risk.** About 8.6% of amateur runners meet exercise addiction criteria. Running technology use correlates with higher injury rates (OR 0.31 for non-users vs. users), potentially through fostering obsessive behavior. An AI coach that provides positive reinforcement, tracks streaks, and generates progressively challenging plans could reinforce compulsive patterns in vulnerable individuals. The mandatory rest days and volume caps partially counteract this, but the motivational coaching layer could work against it. Design for this: detect distress at rest days, excessive override requests, and patterns suggesting compulsive exercise.

**RED-S and nutritional blindspots are a genuine gap.** Training load monitoring cannot detect energy deficiency. An estimated 23–80% of female athletes experience some degree of low energy availability. Among recreational female runners aged 18–25 with multiple stress fractures, 82% were classified as "at risk" for low energy availability. The AI can increase training volume for a user with undetected LEA, accelerating harm. This risk is shared with human coaches who aren't clinicians, but it's worth addressing through intake screening (menstrual health, eating habits, bone density history) and pattern detection.

**Scope creep into medical territory is the conversational risk.** Users will ask the LLM about pain, nutrition, body composition, mental health — topics that cross into medical advice territory regardless of guardrails. Even with system prompts constraining the LLM, conversational AI can drift toward inappropriate reassurance. Hard keyword-based triggers ("chest pain," "persistent pain," "missed periods," "stress fracture") that automatically generate medical referral advice are more reliable than LLM self-policing.

**User misrepresentation creates garbage-in, garbage-out.** Users who underreport pain, misstate fitness levels, or hide medical conditions undermine every guardrail. No system fully prevents this, but the risk should be acknowledged. The assumption of risk defense is strongest when the product clearly communicated what information it needed and the user failed to provide it.

### The incremental risk calculation

Compared to the most common alternative — **self-coaching, used by ~95% of runners** — the product represents a clear risk reduction. Self-coached runners have no load monitoring, no progressive overload enforcement, no mandatory recovery, and no systematic injury prevention. Compared to **static book plans** (Hal Higdon, Daniels, Pfitzinger), the product adds adaptive guardrails that books structurally cannot provide — these plans have safely served millions but cannot respond to individual fatigue, missed workouts, or illness. Compared to **generic ChatGPT**, the product is dramatically safer — ChatGPT has no load monitoring, no guardrails, produces hallucinations, and was never designed for sequential training plan management. Compared to **a human running coach**, the product is more consistent (no bad days, no cognitive biases, systematic load tracking) but less capable of physical observation, nuanced judgment, and detecting psychological distress. The overall risk profile is **comparable to a decent human coach, with different strengths and weaknesses**.

---

## Conclusion: an honest legal posture, not security theater

The law here is genuinely unsettled. *Garcia v. Character Technologies* opened the door to treating AI chatbots as products, but no court has applied this to fitness coaching. The information-vs-product distinction from *Winter v. Putnam* favors the hybrid architecture. The assumption-of-risk doctrine in fitness is robust. The complete absence of coaching-advice litigation against any fitness app is the strongest empirical signal.

The three actions with highest legal ROI are: **(1) mandatory arbitration with class action waiver** in the ToS (prevents the highest-magnitude risk), **(2) FTC Health Breach Notification Rule compliance** (the regulatory requirement most founders miss, with real financial teeth), and **(3) comprehensive logging of the deterministic safety layer's decisions** (proves the guardrails work if ever challenged). Everything else — health screening, disclaimers, insurance — is important but secondary to these three.

The product's architecture is its best legal asset. By separating the deterministic computation layer (auditable, rule-based, safety-enforcing) from the LLM coaching conversation (qualitative, advisory, non-prescriptive), the product naturally aligns with the legal distinction courts draw between functional tools and informational expression. This isn't accidental good fortune — it's a design that produces both better coaching and better legal defensibility. The remaining risk surface is real (RED-S blindspots, exercise addiction reinforcement, scope creep, user misrepresentation) but manageable through screening, design guardrails, and honest positioning. This is sufficient to take to a health-tech attorney and have a productive, specific conversation about implementation.