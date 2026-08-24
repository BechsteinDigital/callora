using Callora.Core.Application.Diagnostics;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Callora.Administration.Api.Admin.Diagnostics;

/// <summary>
/// Turns the plugin execution recorder on for a bounded window and reads what it captured.
/// </summary>
/// <remarks>
/// <para>
/// Answers the one question aggregates cannot. Job, lifecycle and webhook telemetry all say
/// whether the platform is healthy; none says "this request took four seconds, who spent
/// it". Under ADR-013 several foreign plugins share one process and one database
/// connection, so that question has no other source.
/// </para>
/// <para>
/// There is deliberately no way to enable it indefinitely. A recorder capturing every query
/// of every request is a developer tool pointed at a system that matters, and what makes it
/// safe to switch on is that it switches itself off.
/// </para>
/// </remarks>
[ApiController]
[Authorize]
[Route("api/diagnostics/recorder")]
[Produces("application/json")]
[Tags("Diagnostics")]
public sealed class RecorderController : ControllerBase
{
    [HttpPost("start")]
    [CalloraPermission(BackendPermissionKeys.DiagnosticsRecord)]
    [ProducesResponseType<RecorderStatusApiResponse>(StatusCodes.Status200OK)]
    public IActionResult Start(
        [FromBody] StartRecordingApiRequest request,
        [FromServices] PluginExecutionRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recorder);

        var requested = request.WindowSeconds is > 0
            ? TimeSpan.FromSeconds(request.WindowSeconds.Value)
            : PluginExecutionRecorder.MaximumWindow;
        recorder.Start(new RecorderSession(requested, request.PluginId));

        // The EFFECTIVE window is returned, not the requested one. It is clamped, and an
        // operator planning a reproduction needs to know how long they actually have.
        var effective = requested > PluginExecutionRecorder.MaximumWindow
            ? PluginExecutionRecorder.MaximumWindow
            : requested;
        return Ok(new RecorderStatusApiResponse(true, (int)effective.TotalSeconds, request.PluginId, 0));
    }

    [HttpPost("stop")]
    [CalloraPermission(BackendPermissionKeys.DiagnosticsRecord)]
    [ProducesResponseType<RecorderStatusApiResponse>(StatusCodes.Status200OK)]
    public IActionResult Stop([FromServices] PluginExecutionRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(recorder);

        recorder.Stop();
        return Ok(new RecorderStatusApiResponse(false, 0, null, recorder.Recent().Count));
    }

    [HttpGet]
    [CalloraPermission(BackendPermissionKeys.DiagnosticsRecord)]
    [ProducesResponseType<RecordedCommandApiResponse[]>(StatusCodes.Status200OK)]
    public IActionResult Recent([FromServices] PluginExecutionRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(recorder);

        return Ok(recorder.Recent()
            .Select(command => new RecordedCommandApiResponse(
                command.PluginId,
                command.CommandText,
                command.Duration.TotalMilliseconds,
                command.OccurredAtUtc))
            .ToArray());
    }
}
