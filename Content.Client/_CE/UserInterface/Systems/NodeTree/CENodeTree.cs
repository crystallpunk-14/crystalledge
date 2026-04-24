using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Client._CE.UserInterface.Systems.NodeTree;

public sealed class CENodeTreeElement(string nodeKey, bool gained ,bool active, Vector2 uiPosition, SpriteSpecifier? icon = null, string? label = null)
{
    public string NodeKey = nodeKey;
    public bool Gained = gained;
    public bool Active = active;

    public Vector2 UiPosition = uiPosition;
    public SpriteSpecifier? Icon = icon;

    /// <summary>
    /// Optional short text label drawn in the top-right corner of the node icon
    /// (e.g. a player counter for the admin dungeon overview).
    /// </summary>
    public string? Label = label;
}

public sealed class CENodeTreeUiState(
    HashSet<CENodeTreeElement> nodes,
    HashSet<(string, string)>? edges = null,
    SpriteSpecifier? frameIcon = null,
    SpriteSpecifier? hoveredIcon = null,
    SpriteSpecifier? selectedIcon = null,
    SpriteSpecifier? learnedIcon = null,
    Color? lineColor = null,
    Color? activeLineColor = null
    ) : BoundUserInterfaceState
{
    public HashSet<CENodeTreeElement> Nodes = nodes;
    public HashSet<(string, string)> Edges = edges ?? new HashSet<(string, string)>();

    public SpriteSpecifier FrameIcon = frameIcon ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_CE/Interface/NodeTree/default.rsi"), "frame");
    public SpriteSpecifier HoveredIcon = hoveredIcon ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_CE/Interface/NodeTree/default.rsi"), "hovered");
    public SpriteSpecifier SelectedIcon = selectedIcon?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_CE/Interface/NodeTree/default.rsi"), "selected");
    public SpriteSpecifier LearnedIcon = learnedIcon?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_CE/Interface/NodeTree/default.rsi"), "learned");

    public Color LineColor = lineColor ?? Color.Gray;
    public Color ActiveLineColor = activeLineColor ?? Color.White;

    /// <summary>
    /// If true, edges are treated as directed (<c>Item1 -&gt; Item2</c>) and an arrowhead
    /// is drawn near <c>Item2</c> to indicate direction.
    /// </summary>
    public bool DirectedEdges = false;
}
