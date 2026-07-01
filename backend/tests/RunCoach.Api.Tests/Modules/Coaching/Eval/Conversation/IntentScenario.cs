using RunCoach.Api.Modules.Coaching.Conversation;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Conversation;

/// <summary>
/// One labelled message in the conversation intent-classifier ground-truth set
/// (Slice 4B Unit 7). <see cref="Expected"/> is the physiologically- and
/// product-correct class the classifier must reproduce: the committed labels ARE
/// the zero-regression baseline (mirrors <c>EscalationScenario</c> in the
/// adaptation suite). <see cref="Id"/> doubles as the byte-stable eval-cache
/// scenario suffix, so it must be unique and stable across runs.
/// </summary>
/// <param name="Id">A stable, unique, dot-delimited scenario id (also the cache key suffix).</param>
/// <param name="Message">The raw runner message handed to the classifier (sanitized + spotlight-wrapped inside the eval).</param>
/// <param name="Expected">The ground-truth intent the classifier must resolve.</param>
internal sealed record IntentScenario(string Id, string Message, MessageIntent Expected);
