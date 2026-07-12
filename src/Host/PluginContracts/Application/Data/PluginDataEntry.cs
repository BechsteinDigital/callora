namespace Callora.Host.PluginContracts.Application.Data;

/// <summary>
/// One stored plugin data document.
/// </summary>
/// <param name="EntryKey">Entry identifier unique within the collection.</param>
/// <param name="JsonDocument">Raw JSON document payload.</param>
/// <param name="UpdatedAtUtc">Timestamp of the last write.</param>
public sealed record PluginDataEntry(
    string EntryKey,
    string JsonDocument,
    DateTimeOffset UpdatedAtUtc);
