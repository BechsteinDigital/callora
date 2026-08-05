namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>One outstanding call-event ticket: which workspace it opens, and when it was minted.</summary>
/// <param name="WorkspaceKey">The workspace whose calls the socket will carry.</param>
/// <param name="MintedAt">Mint time; the ticket expires relative to it.</param>
internal readonly record struct CallEventTicket(string WorkspaceKey, DateTimeOffset MintedAt);
