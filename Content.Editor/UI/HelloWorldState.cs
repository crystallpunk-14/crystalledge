using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Editor.UI;

/// <summary>
/// Minimal "Hello World" state to verify that the lightweight editor shell works.
/// </summary>
public sealed class HelloWorldState : State
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private BoxContainer _root = default!;

    protected override void Startup()
    {
        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var center = new CenterContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var label = new Label
        {
            Text = "Hello, World!",
            FontColorOverride = Color.White,
        };

        center.AddChild(label);
        _root.AddChild(center);
        _uiManager.StateRoot.AddChild(_root);
    }

    protected override void Shutdown()
    {
        _root.Dispose();
    }
}
