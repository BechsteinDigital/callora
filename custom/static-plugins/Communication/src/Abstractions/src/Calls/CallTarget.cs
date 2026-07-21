namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Channel-neutral address of one call participant, for example a phone number or user handle.
/// The owning channel translates the value into its protocol-specific address format.
/// </summary>
/// <param name="Value">Raw target value, for example "+49301234567".</param>
/// <param name="DisplayName">Optional human-readable participant name.</param>
public sealed record CallTarget(string Value, string? DisplayName = null);
