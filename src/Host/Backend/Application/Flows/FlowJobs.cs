namespace Callora.Host.Backend.Application.Flows;

/// <summary>Stable job type names for the flow engine.</summary>
public static class FlowJobs
{
    /// <summary>Durable job that executes one matched flow.</summary>
    public const string ExecuteJobType = "flow.execute";
}
