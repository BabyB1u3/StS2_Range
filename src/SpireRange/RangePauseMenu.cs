using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.addons.mega_text;

namespace SpireRange;

internal static class RangePauseMenu
{
    private static NPauseMenu? _menu;
    private static NPauseMenuButton? _reset;
    private static NPauseMenuButton? _exit;

    public static void Apply(NPauseMenu menu)
    {
        if (!RangeSession.IsActive) return;
        if (!ReferenceEquals(_menu, menu))
            menu.TreeExiting += () =>
            {
                if (!ReferenceEquals(_menu, menu)) return;
                _menu = null; _reset = null; _exit = null;
            };
        _menu = menu;
        var giveUp = NativeAccess.Get<NPauseMenuButton>(menu, "_giveUpButton");
        giveUp.Visible = false;
        giveUp.Disable();
        // Single-player hides this native button. Reuse its slot, texture, hover
        // animation and existing navigation between GiveUp and SaveAndQuit.
        _reset = NativeAccess.Get<NPauseMenuButton>(menu, "_disconnectButton");
        _exit = NativeAccess.Get<NPauseMenuButton>(menu, "_saveAndQuitButton");
        _reset.Visible = true;
        _reset.GetNode<MegaLabel>("Label").SetTextAutoSize("重置靶场");
        _exit.GetNode<MegaLabel>("Label").SetTextAutoSize("退出靶场");
        var buttons = NativeAccess.Get<Control>(menu, "_buttonContainer").GetChildren()
            .OfType<NPauseMenuButton>().Where(b => b.Visible).ToArray();
        for (var i = 0; i < buttons.Length; i++)
        {
            var previous = buttons[(i + buttons.Length - 1) % buttons.Length].GetPath();
            var next = buttons[(i + 1) % buttons.Length].GetPath();
            buttons[i].FocusNeighborTop = buttons[i].FocusPrevious = previous;
            buttons[i].FocusNeighborBottom = buttons[i].FocusNext = next;
        }
        RefreshAvailability();
    }

    public static void RefreshAvailability()
    {
        if (!RangeSession.IsActive || _menu is null || !GodotObject.IsInstanceValid(_menu)
            || !_menu.Visible || _reset is null || _exit is null) return;
        foreach (var button in new[] { _reset, _exit })
        {
            var enabled = RangeSession.CanResetOrExit;
            if (enabled == button.IsEnabled) continue;
            if (enabled) button.Enable(); else button.Disable();
        }
    }

    public static bool UsePracticeAction(bool reset)
    {
        if (!RangeSession.IsActive) return false;
        _ = RangeUi.RunBattleAction(reset);
        return true;
    }
}

[HarmonyPatch(typeof(NPauseMenu), "OnGiveUpButtonPressed")]
internal static class RangePauseGiveUpPatch
{
    private static bool Prefix() => !RangeSession.IsActive;
}

[HarmonyPatch(typeof(NPauseMenu), nameof(NPauseMenu.Initialize))]
internal static class RangePauseInitializePatch
{
    private static void Postfix(NPauseMenu __instance) => RangePauseMenu.Apply(__instance);
}

[HarmonyPatch(typeof(NPauseMenu), "RefreshLabels")]
internal static class RangePauseLabelsPatch
{
    private static void Postfix(NPauseMenu __instance) => RangePauseMenu.Apply(__instance);
}

[HarmonyPatch(typeof(NPauseMenu), nameof(NPauseMenu.OnSubmenuOpened))]
internal static class RangePauseOpenedPatch
{
    private static void Postfix(NPauseMenu __instance) => RangePauseMenu.Apply(__instance);
}

[HarmonyPatch(typeof(NPauseMenu), "OnDisconnectButtonPressed")]
internal static class RangePauseResetPatch
{
    private static bool Prefix() => !RangePauseMenu.UsePracticeAction(reset: true);
}

[HarmonyPatch(typeof(NPauseMenu), "OnSaveAndQuitButtonPressed")]
internal static class RangePauseExitPatch
{
    private static bool Prefix() => !RangePauseMenu.UsePracticeAction(reset: false);
}
