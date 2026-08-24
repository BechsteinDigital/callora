using Callora.Core.Extensibility;
namespace Callora.Core.Application.Diagnostics;

/// <summary>
/// What one recording run captures and for how long.
/// </summary>
/// <param name="Window">
/// How long recording stays on. Clamped to
/// <see cref="PluginExecutionRecorder.MaximumWindow"/>.
/// </param>
/// <param name="PluginId">
/// Record only this plugin's work; null records every plugin. Narrowing matters more than
/// it looks: a busy host fills the ring in seconds, and the request being investigated is
/// then already gone.
/// </param>
[CalloraInternal("Operator diagnostics — not a plugin contract (REV2 §7.2)")]
public sealed record RecorderSession(TimeSpan Window, string? PluginId = null);
