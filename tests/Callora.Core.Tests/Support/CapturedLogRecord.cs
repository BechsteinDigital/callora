using System.Diagnostics;

namespace Callora.Core.Tests.Support;

/// <summary>Ein exportierter Log-Eintrag, aus dem gepoolten Original herauskopiert.</summary>
public sealed record CapturedLogRecord(
    string? FormattedMessage,
    ActivityTraceId TraceId,
    ActivitySpanId SpanId,
    IReadOnlyList<KeyValuePair<string, object?>> Scopes);
