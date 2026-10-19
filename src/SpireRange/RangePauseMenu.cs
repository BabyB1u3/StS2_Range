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
        // Single-player hides this native button. Reuse its slot, texture, hover
        // animation and existing navigation between GiveUp and SaveAndQuit.
        _reset = NativeAccess.Get<NPauseMenuButton>(menu, "_disconnectButton");
        _exit = NativeAccess.Get<NPauseMenuButton>(menu, "_saveAndQuitButton");
        _reset.Visible = true;
        _reset.GetNode<MegaLabel>("Label").SetTextAutoSize("重置测试");
        _exit.GetNode<MegaLabel>("Label").SetTextAutoSize("退出靶场");
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
