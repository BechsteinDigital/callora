namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Who an inbound-call owner is, for the places a call's story is told.
/// </summary>
/// <remarks>
/// Owners are objects, and an object cannot be shown to an operator. Without a name the answer to
/// "who answered this call" is "something did" — true and useless. It is deliberately <em>not</em> an
/// authorisation subject: nothing decides anything on this, it only makes the record readable.
/// </remarks>
/// <param name="Id">Stable machine name, usually the owning plugin's id.</param>
/// <param name="DisplayName">What an operator reads — the function, not the class.</param>
public sealed record CallOwnerIdentity(string Id, string DisplayName)
{
    /// <summary>
    /// What an owner that names nothing is reported as. Honest rather than invented: a consumer
    /// written before identities existed still works, and the record says exactly that much.
    /// </summary>
    public static CallOwnerIdentity Anonymous { get; } = new("unknown", "an unnamed consumer");
}
