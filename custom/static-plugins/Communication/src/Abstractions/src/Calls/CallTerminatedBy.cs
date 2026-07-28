namespace Callora.Plugin.Communication.Abstractions;

/// <summary>Which side ended a call.</summary>
public enum CallTerminatedBy
{
    /// <summary>The local endpoint ended the call.</summary>
    Local,

    /// <summary>The remote party ended the call.</summary>
    Remote,

    /// <summary>The ending side could not be determined.</summary>
    Unknown,
}
