using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace SpireRange;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public static void Initialize()
    {
        new Harmony("StS2Range.SpireRange").PatchAll();
        GD.Print("[SpireRange] Loaded 0.1.3 (target: v0.107.1)");
    }
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class MainMenuPatch
{
    private static void Postfix(NMainMenu __instance) => RangeUi.AttachMenu(__instance);
}

[HarmonyPatch(typeof(NRun), nameof(NRun._Ready))]
internal static class RunUiPatch
{
    private static void Postfix(NRun __instance)
    {
        if (RangeSession.IsActive)
            RangeUi.AttachToolbar(__instance);
    }
}
