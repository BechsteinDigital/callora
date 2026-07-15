namespace Callora.Core.Application.Flows;

/// <summary>Stable job type names for the flow engine.</summary>
public static class FlowJobs
{
    /// <summary>Durable job that executes one matched flow.</summary>
    public const string ExecuteJobType = "flow.execute";
}
