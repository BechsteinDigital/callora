namespace Callora.Core.Application.Extensions;

/// <summary>A region inside a section layout — one slot a block can sit in.</summary>
/// <param name="RegionKey">What the renderer writes into <c>data-cal-region</c>.</param>
/// <param name="Label">What the editor shows.</param>
public sealed record SectionLayoutRegion(string RegionKey, string Label);
