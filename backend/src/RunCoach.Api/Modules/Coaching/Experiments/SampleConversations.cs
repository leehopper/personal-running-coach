using System.Collections.Immutable;
using RunCoach.Api.Modules.Coaching.Models;

namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Provides sample conversation histories for experiment variations.
/// These are realistic coaching conversations that test whether prior
/// context improves plan generation quality and coherence.
/// </summary>
public static class SampleConversations
{
    /// <summary>
    /// Gets an empty conversation history (0 turns) for cold start testing.
    /// </summary>
    public static ImmutableArray<ConversationTurn> Empty { get; } =
        ImmutableArray<ConversationTurn>.Empty;

    /// <summary>
    /// Gets a 5-turn coaching conversation where an intermediate runner
    /// discusses training goals, current fitness, and schedule constraints.
    /// Suitable for Lee-type profiles (intermediate, race goal).
    /// </summary>
    public static ImmutableArray<ConversationTurn> IntermediateGoalSetting { get; } =
    [
        new ConversationTurn(
            "Hi, I'm training for a half marathon in about 16 weeks. I've been running consistently for about 3 years and just ran a 48-minute 10K. What kind of plan would work for me?",
            "Great to meet you! A 48-minute 10K is a solid foundation — that tells me you've built good aerobic fitness. For a 16-week half marathon block, I'd typically structure this in 3-4 phases: a base-building phase to solidify your aerobic foundation, a strength/tempo phase to build lactate threshold fitness, a sharpening phase for race-specific work, and a taper. What's your current weekly mileage looking like?"),

        new ConversationTurn(
            "I'm doing about 40km per week right now. Usually 4-5 runs. My long run is around 14km.",
            "That's a really good starting point for half marathon training. With 40km per week and a 14km long run, you've already got the base volume that many plans build toward. I'd look at gradually progressing to around 50-55km per week at peak, with your long run building to 18-20km. The key will be adding quality sessions — tempo runs and some interval work — while keeping most of your running easy. Do you have a time goal for the half, or are you focused on finishing strong?"),

        new ConversationTurn(
            "I'd love to break 1:45 if possible. Is that realistic based on my 10K time?",
            "Based on your 48-minute 10K, a sub-1:45 half marathon is definitely within reach with good training — race equivalency tables would predict something in the 1:42-1:46 range. The key is that half marathon performance is very aerobic, so the base and threshold work will be the biggest levers. I'd say 1:45 is a realistic A goal, with a B goal of 1:48 as a safety net. How does your schedule look for training? Any days that are off-limits?"),

        new ConversationTurn(
            "I can run Monday, Wednesday, Friday, Saturday, and Sunday. I have about 75 minutes max for weekday runs. Weekends are more flexible.",
            "Perfect — 5 days gives us great flexibility. I'd structure it as: Monday easy, Wednesday as your primary quality session (tempo or intervals), Friday easy, Saturday as a secondary quality or easy day depending on the phase, and Sunday for your long run. The 75-minute weekday cap works well — most quality sessions including warm-up and cool-down fit comfortably in that window. One more thing — any injury history I should know about?"),

        new ConversationTurn(
            "I had some IT band issues about 6 months ago but it's been fine since. I've been doing strength work to prevent it.",
            "Good on you for being proactive with the strength work — that's exactly the right approach for IT band management. I'll keep that in mind when structuring the plan. We'll make sure to build volume gradually and include deload weeks every 3-4 weeks to manage cumulative fatigue. The strength work you're already doing is a great complement. I've got a good picture of where you are — ready for me to put together your plan?"),
    ];

    /// <summary>
    /// Gets a 5-turn coaching conversation where a beginner runner
    /// discusses getting started with running. Suitable for Sarah-type profiles.
    /// </summary>
    public static ImmutableArray<ConversationTurn> BeginnerGettingStarted { get; } =
    [
        new ConversationTurn(
            "Hi, I've been running for about 6 months and I'm thinking about doing my first 5K race. Is that too ambitious?",
            "Not at all — that's a wonderful goal! Six months of running gives you a solid foundation, and a 5K is a fantastic first race distance. It's long enough to feel like a real achievement but short enough that you won't need to dramatically change your current training. What does your running look like right now? How often and how far are you typically going?"),

        new ConversationTurn(
            "I run 3 times a week, about 5km each time. Some days are harder than others but I can usually get through it.",
            "That's a great starting point! 15km per week with consistent 3-day frequency means you already have the fitness to complete a 5K. The race itself will feel different from training — the adrenaline and other runners around you often make it feel easier at the start. Our focus would be on building your confidence and making sure you're comfortable on race day. When is the race?"),

        new ConversationTurn(
            "It's in about 8 weeks. A local parkrun 5K.",
            "Eight weeks is plenty of time. Parkruns are such a great first race experience — they're friendly, well-organized, and everyone from beginners to experienced runners shows up. For your plan, I'd focus on gradually making your 3 runs per week a bit more structured: one easy run, one slightly longer run to build endurance, and one run where you play with pace a bit. No need to add more days — 3 is perfect for where you are. Do you have a time goal or are you just looking to finish feeling strong?"),

        new ConversationTurn(
            "No time goal really. I just want to finish without walking if I can. Sometimes I still need walking breaks on my runs.",
            "That's a great goal, and very achievable in 8 weeks. And I want to say — there is absolutely nothing wrong with walking breaks. Many experienced runners use run-walk strategies in training and even in races. If you need a walking break during the race, that's completely fine. But if running continuously is your goal, we can work toward that gradually. The key is keeping your easy runs truly easy — slow enough that you could hold a conversation. Most new runners go too fast on their easy days, which makes them need those walking breaks."),

        new ConversationTurn(
            "That makes sense. I think I do run too fast sometimes because I feel like I should be faster. How slow is too slow for easy runs?",
            "There is no such thing as too slow on an easy day! I mean that genuinely — the purpose of easy runs is to build aerobic fitness while keeping stress low. If you can chat comfortably, you're in the right zone. For many runners at your stage, that might be around 7:00-8:00 per km, but the feel matters more than the number. Think of easy runs as a 'moving massage' — they should leave you feeling better than when you started. Ready for me to put together a simple 8-week plan?"),
    ];

    /// <summary>
    /// Builds a conversation history with the specified number of turns
    /// from the intermediate goal setting conversation.
    /// Returns empty if turns is 0, or the first N turns from the sample.
    /// </summary>
    /// <param name="turns">Number of conversation turns (0-5).</param>
    /// <returns>The conversation history with the requested number of turns.</returns>
    public static ImmutableArray<ConversationTurn> GetIntermediateTurns(int turns)
    {
        if (turns <= 0)
        {
            return Empty;
        }

        var count = Math.Min(turns, IntermediateGoalSetting.Length);
        return [.. IntermediateGoalSetting.Take(count)];
    }

    /// <summary>
    /// Builds a conversation history with the specified number of turns
    /// from the beginner getting started conversation.
    /// Returns empty if turns is 0, or the first N turns from the sample.
    /// </summary>
    /// <param name="turns">Number of conversation turns (0-5).</param>
    /// <returns>The conversation history with the requested number of turns.</returns>
    public static ImmutableArray<ConversationTurn> GetBeginnerTurns(int turns)
    {
        if (turns <= 0)
        {
            return Empty;
        }

        var count = Math.Min(turns, BeginnerGettingStarted.Length);
        return [.. BeginnerGettingStarted.Take(count)];
    }
}
