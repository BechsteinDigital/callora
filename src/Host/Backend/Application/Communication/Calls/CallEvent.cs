namespace Callora.Host.Backend.Application.Communication.Calls;

/// <summary>
/// One event on the call event stream, for example a new ringing call or a
/// state transition. See <see cref="CallEventTypes"/> for the type codes.
/// </summary>
public sealed record CallEvent(string Type, ActiveCallSnapshot Call);
