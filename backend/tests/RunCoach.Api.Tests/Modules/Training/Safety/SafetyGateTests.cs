using System.Collections.Immutable;
using System.Text.RegularExpressions;
using FluentAssertions;
using RunCoach.Api.Modules.Training.Constants;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Training.Safety;

/// <summary>
/// The deterministic <see cref="SafetyGate"/> classification contract (Slice 3
/// Unit 3, safety-gate.feature): crisis ⇒ Red/Crisis, emergency ⇒
/// Red/EmergencyReferral, injury ⇒ Amber/Injury, RED-S ⇒ Amber/RedS, benign ⇒
/// Green, highest-tier-wins, free-text-metric scanning, multi-line proximity,
/// and false-positive guards for running hyperbole and accidental injury.
/// </summary>
public sealed class SafetyGateTests
{
    private readonly SafetyGate _sut = new();

    [Theory]
    [InlineData("I keep thinking I want to kill myself")]
    [InlineData("honestly I just want to end my life")]
    [InlineData("thought about ending it all out there")]
    [InlineData("I've had suicidal thoughts on every run lately")]
    [InlineData("kept thinking about suicide out there")]
    [InlineData("I've been self-harming again")]
    [InlineData("part of me wants to hurt myself")]
    [InlineData("some days I don't want to be here anymore")]
    [InlineData("everyone would be better off without me")]
    [InlineData("there's no reason to keep going")]
    [InlineData("I can't go on like this")]
    [InlineData("I just wish I wouldn't wake up tomorrow")]

    // Intent-qualified self-harm: `SG-C08` and `SG-C09` (the negative side of
    // these rules — "tripped and hurt myself", "cut myself on a branch" — is
    // covered by the benign theory; these assert the positive escalation).
    [InlineData("I keep thinking about hurting myself")]
    [InlineData("I cut myself on purpose after the run")]
    [InlineData("been cutting myself again")]
    public void Classify_ReturnsRedCrisis_ForCrisisNotes(string notes)
    {
        // Act
        var actual = _sut.Classify(notes, null);

        // Assert
        actual.Should().Be(SafetyClassification.Red(ReferralCategory.Crisis));
    }

    [Theory]
    [InlineData("bad chest pain in the last mile and had to walk")]
    [InlineData("chest felt tight and heavy near the end")]
    [InlineData("tightness in my chest going up the hill")]
    [InlineData("chest pressure during the tempo")]
    [InlineData("noticed an irregular heartbeat afterwards")]
    [InlineData("my heart kept skipping during the tempo")]
    [InlineData("pain radiating down my left arm")]
    [InlineData("felt like I was going to faint")]
    [InlineData("I passed out right after finishing")]
    [InlineData("the physio mentioned a femoral neck issue")]
    [InlineData("doctor said it might be a femoral stress fracture")]
    [InlineData("deep groin pain that would not settle")]
    [InlineData("groin hurts more every run now")]
    [InlineData("deep ache in my hip that gets worse when I run")]
    [InlineData("I'm pregnant and started bleeding after the run")]
    [InlineData("28 weeks pregnant and having contractions tonight")]
    [InlineData("20 weeks along and spotting after the run")]
    [InlineData("I'm expecting and had some bleeding today")]

    // Isolates `SG-E13` (bleeding with no pregnancy marker — every other bleeding
    // row carries one and matches `SG-E14`/`SG-E15` instead) and the reverse-order
    // proximity rules `SG-E10`, `SG-E12`, `SG-E15` that the forward-order rows
    // above never reach (first-match returns on the forward sibling otherwise).
    [InlineData("heavy vaginal bleeding during the run")]
    [InlineData("deep pain in the groin after the run")]
    [InlineData("felt a stabbing in my hip")]
    [InlineData("bleeding badly, I am 12 weeks pregnant")]
    public void Classify_ReturnsRedEmergencyReferral_ForEmergencyNotes(string notes)
    {
        // Act
        var actual = _sut.Classify(notes, null);

        // Assert
        actual.Should().Be(SafetyClassification.Red(ReferralCategory.EmergencyReferral));
    }

    [Theory]
    [InlineData("sharp pain in my knee the whole way")]
    [InlineData("shooting pain in my shin again")]
    [InlineData("stabbing pain in my calf, no good")]
    [InlineData("persistent pain in my shin again")]
    [InlineData("the pain is getting worse each run")]
    [InlineData("my achilles is really painful, getting worse")]
    [InlineData("had to stop because my ankle was hurting")]
    [InlineData("had to cut it short, knee was killing me")]
    [InlineData("couldn't finish, ankle gave out")]
    [InlineData("couldn't run, can't put weight on it")]

    // Isolates the reverse-order proximity rules `SG-I04` (worsening before pain)
    // and `SG-I06` (pain before run-curtailment) that the forward-order rows above
    // never reach.
    [InlineData("it is getting worse, real pain now")]
    [InlineData("the pain got so bad I had to stop")]
    public void Classify_ReturnsAmberInjury_ForInjuryNotes(string notes)
    {
        // Act
        var actual = _sut.Classify(notes, null);

        // Assert
        actual.Should().Be(SafetyClassification.Amber(ReferralCategory.Injury));
    }

    [Theory]
    [InlineData("dealing with amenorrhea for a while now")]
    [InlineData("I've missed my last three periods")]
    [InlineData("haven't had a period in months")]
    [InlineData("my period stopped a few months ago")]
    [InlineData("lost my period since I upped mileage")]
    [InlineData("just got a stress fracture diagnosis")]
    [InlineData("coach says I'm not eating enough")]
    [InlineData("only went out to earn my dinner")]
    [InlineData("ate too much so I'll just run it off")]
    [InlineData("ran through the pain like always")]
    [InlineData("felt so guilty about taking a rest day")]

    // `SG-R09` compensatory-exercise positive (the sibling `SG-R08` "run it off"
    // is already covered above) and the reverse-order proximity rule `SG-R12`
    // (rest-day before distress).
    [InlineData("ate too much so I went out to burn it off")]
    [InlineData("took a rest day and felt so guilty afterwards")]
    public void Classify_ReturnsAmberRedS_ForDisorderedPatternNotes(string notes)
    {
        // Act
        var actual = _sut.Classify(notes, null);

        // Assert
        actual.Should().Be(SafetyClassification.Amber(ReferralCategory.RedS));
    }

    // Benign running talk, hyperbole, accidental injury, and empty notes must
    // never escalate. These guard the catalog's false-positive discipline — note
    // the omitted bare "want to die", the intent-required self-injury rules
    // ("tripped and hurt myself" stays Green), and the navigational "ran it off
    // the road" guard.
    [Theory]
    [InlineData("easy 5k, felt great on a beautiful morning")]
    [InlineData("tempo done, legs a little tired but fine")]
    [InlineData("long run complete, fueled well throughout")]
    [InlineData("recovery jog, nice and relaxed")]
    [InlineData("that final hill absolutely killed me")]
    [InlineData("dead legs the entire way today")]
    [InlineData("killed that tempo, felt strong")]
    [InlineData("honestly this pace is murder")]
    [InlineData("cutting back my mileage this week")]
    [InlineData("I wanted to die on that last climb it was so steep")]
    [InlineData("felt some muscle contractions in my calf")]
    [InlineData("ran through the finish line feeling strong")]
    [InlineData("took a rest day and came back recovered")]
    [InlineData("burned a ton of calories on the long run")]
    [InlineData("tripped and hurt myself on the trail")]
    [InlineData("cut myself on a branch mid-run")]
    [InlineData("ran it off the road onto the trail")]
    [InlineData("hip felt a little sore after the long run")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_ReturnsGreen_ForBenignHyperbolicAndEmptyNotes(string? notes)
    {
        // Act
        var actual = _sut.Classify(notes, null);

        // Assert
        actual.Should().Be(SafetyClassification.Green());
    }

    [Fact]
    public void Classify_MatchesAcrossNewlinesAndSentenceBoundaries()
    {
        // Arrange — a pregnancy emergency split across a sentence boundary and a
        // newline. The proximity bridge must still bridge it (multi-line notes
        // are common); a class that broke on "." or "\n" would silently miss this.
        const string notes = "I'm 28 weeks pregnant.\nStarted bleeding heavily after the run.";

        // Act
        var actual = _sut.Classify(notes, null);

        // Assert
        actual.Should().Be(SafetyClassification.Red(ReferralCategory.EmergencyReferral));
    }

    [Fact]
    public void Classify_HighestTierWins_WhenCrisisAndInjuryBothPresent()
    {
        // Arrange — a Red crisis signal and an Amber injury signal in one note.
        const string notes = "sharp pain in my knee and honestly I want to kill myself";

        // Act
        var actual = _sut.Classify(notes, null);

        // Assert — Red crisis outranks Amber injury.
        actual.Should().Be(SafetyClassification.Red(ReferralCategory.Crisis));
    }

    [Fact]
    public void Classify_ScansFreeTextMetricValues()
    {
        // Arrange — a crisis phrase smuggled into the free-text weather metric.
        var metrics = new Dictionary<string, string>
        {
            [WorkoutMetricKeys.Weather] = "felt like I want to kill myself out there",
        };

        // Act
        var actual = _sut.Classify(notes: null, metrics);

        // Assert
        actual.Should().Be(SafetyClassification.Red(ReferralCategory.Crisis));
    }

    [Fact]
    public void Classify_DoesNotScanNumericMetricValues()
    {
        // Arrange — numeric metric values are not user prose and are not scanned.
        var metrics = new Dictionary<string, string>
        {
            [WorkoutMetricKeys.HrAvg] = "I want to kill myself",
        };

        // Act
        var actual = _sut.Classify(notes: "good run", metrics);

        // Assert
        actual.Should().Be(SafetyClassification.Green());
    }

    [Fact]
    public void Classify_FailsClosed_WhenAGateRuleTimesOut()
    {
        // Arrange — a poison rule whose matcher always times out (catastrophic
        // backtracking on `(a+)+$`), mirroring the `LayeredPromptSanitizer` ReDoS
        // guard test. A rule the gate cannot evaluate must escalate, not silently
        // classify Green (DEC-079 recall-over-precision: under-reaction is the
        // hard-failure mode).
        var poison = new SafetyKeywordCatalog.SafetyRule(
            "SG-TEST-TIMEOUT",
            SafetyTier.Red,
            ReferralCategory.Crisis,
            new Regex("(a+)+$", RegexOptions.Compiled, TimeSpan.FromTicks(1)));
        var gate = new SafetyGate(SafetyKeywordCatalog.ForTesting(ImmutableArray.Create(poison)));
        var adversarialInput = new string('a', 30) + "!";

        // Act
        var actual = gate.Classify(adversarialInput, null);

        // Assert — the timed-out rule escalates to its tier (fail closed); no throw.
        actual.Should().Be(SafetyClassification.Red(ReferralCategory.Crisis));
    }
}
