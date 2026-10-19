using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace SpireRange;

internal static class RangeMenu
{
    public static void Attach(NMainMenu menu)
    {
        RangeSession.OnMainMenuReached();
        var list = menu.GetNode<VBoxContainer>("MainMenuTextButtons");
        if (list.HasNode("SpireRangeButton")) return;
        var multiplayer = list.GetNode<NMainMenuTextButton>("MultiplayerButton");
        var button = (NMainMenuTextButton)multiplayer.Duplicate(
            (int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts | Node.DuplicateFlags.UseInstantiation));
        button.Name = "SpireRangeButton";
        list.AddChild(button);
        list.MoveChild(button, multiplayer.GetIndex() + 1);
        LocManager.Instance.GetTable("main_menu_ui").MergeWith(new Dictionary<string, string>
        {
            ["SPIRE_RANGE"] = LocManager.Instance.Language.StartsWith("zh") ? "靶场" : "Practice Range"
        });
        button.SetLocalization("SPIRE_RANGE");
        button.Enable();
        button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ =>
        {
            var screen = RangeSetupScreen.Create(menu);
            menu.SubmenuStack.AddChild(screen);
            menu.SubmenuStack.Push(screen);
        }));
        button.Connect(NClickableControl.SignalName.Focused,
            Callable.From<NMainMenuTextButton>(b => NativeAccess.Call(menu, "MainMenuButtonFocused", b)));
        button.Connect(NClickableControl.SignalName.Unfocused,
            Callable.From<NMainMenuTextButton>(b => NativeAccess.Call(menu, "MainMenuButtonUnfocused", b)));
        list.SortChildren += () => UpdateNavigation(list);
        UpdateNavigation(list);
    }

    private static void UpdateNavigation(VBoxContainer list)
    {
        var buttons = list.GetChildren().OfType<NMainMenuTextButton>().Where(b => b.Visible).ToArray();
        for (var i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            b.FocusNeighborTop = buttons[Math.Max(0, i - 1)].GetPath();
            b.FocusNeighborBottom = buttons[Math.Min(buttons.Length - 1, i + 1)].GetPath();
            b.FocusPrevious = buttons[(i + buttons.Length - 1) % buttons.Length].GetPath();
            b.FocusNext = buttons[(i + 1) % buttons.Length].GetPath();
        }
    }
}
