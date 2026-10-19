using Godot;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace SpireRange;

[HarmonyPatch(typeof(NInspectCardScreen), "UpdateCardDisplay")]
internal static class RangeCardDetailUpdatedPatch
{
    private static void Postfix(NInspectCardScreen __instance) => RangeCompendiumPickers.DetailUpdated(__instance);
}

[HarmonyPatch(typeof(NInspectCardScreen), nameof(NInspectCardScreen.Close))]
internal static class RangeCardDetailClosedPatch
{
    private static void Postfix(NInspectCardScreen __instance) => RangeCompendiumPickers.DetailClosed(__instance);
}

// Keep the native text/description/upgrade/keyword search. Only its discovery
// check changes, and only when the closure belongs to our independent picker.
[HarmonyPatch]
internal static class RangeCardSearchPatch
{
    private static MethodBase TargetMethod() => typeof(NCardLibrary)
        .GetNestedTypes(BindingFlags.NonPublic)
        .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        .Single(m => m.Name.Contains("g__TextFilter|", StringComparison.Ordinal));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var contains = AccessTools.Method(typeof(HashSet<ModelId>), nameof(HashSet<ModelId>.Contains));
        var owner = AccessTools.Field(original.DeclaringType, "<>4__this");
        foreach (var instruction in instructions)
        {
            if (!instruction.Calls(contains)) { yield return instruction; continue; }
            var load = new CodeInstruction(OpCodes.Ldarg_0);
            load.labels.AddRange(instruction.labels);
            load.blocks.AddRange(instruction.blocks);
            yield return load;
            yield return new CodeInstruction(OpCodes.Ldfld, owner);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RangeCardSearchPatch), nameof(IsDiscovered)));
        }
    }

    private static bool IsDiscovered(HashSet<ModelId> discovered, ModelId id, NCardLibrary owner) =>
        RangeCompendiumPickers.IsPicker(owner) || discovered.Contains(id);
}

[HarmonyPatch(typeof(NCardLibrary), "ShowCardDetail")]
internal static class RangeCardPickPatch
{
    private static bool Prefix(NCardLibrary __instance, NCardHolder holder) =>
        !RangeCompendiumPickers.SelectCard(__instance, holder);
}

[HarmonyPatch(typeof(NCardLibraryGrid), nameof(NCardLibraryGrid.RefreshVisibility))]
internal static class RangeCardVisibilityPatch
{
    private static void Postfix(NCardLibraryGrid __instance) => RangeCompendiumPickers.RefreshCardVisibility(__instance);
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary.OnSubmenuOpened))]
internal static class RangeCardOpenedPatch
{
    private static void Postfix(NCardLibrary __instance) => RangeCompendiumPickers.Opened(__instance);
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary.OnSubmenuClosed))]
internal static class RangeCardClosedPatch
{
    private static void Prefix(NCardLibrary __instance) => RangeCompendiumPickers.Closing(__instance);
}

[HarmonyPatch(typeof(NCardLibrary), "DisplayCards")]
internal static class RangeCardDisplayPatch
{
    private static bool Prefix(NCardLibrary __instance, ref Task __result)
    {
        if (!RangeCompendiumPickers.IsPicker(__instance)) return true;
        __result = RangeCompendiumPickers.DisplayCards(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(NBestiary), nameof(NBestiary.OnSubmenuOpened))]
internal static class RangeBestiaryOpenedPatch
{
    private static void Postfix(NBestiary __instance) => RangeCompendiumPickers.Opened(__instance);
}

[HarmonyPatch(typeof(NBestiary), nameof(NBestiary.OnSubmenuClosed))]
internal static class RangeBestiaryClosedPatch
{
    private static void Prefix(NBestiary __instance) => RangeCompendiumPickers.Closing(__instance);
}

[HarmonyPatch(typeof(NBestiary), "CreateEntries")]
internal static class RangeBestiaryEntriesPatch
{
    private static bool Prefix(NBestiary __instance)
    {
        if (!RangeCompendiumPickers.IsPicker(__instance)) return true;
        RangeCompendiumPickers.CreateMonsterEntries(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(NBestiary), "SelectMonster")]
internal static class RangeBestiarySelectionPatch
{
    private static void Postfix(NBestiary __instance, NBestiaryEntry entry) => RangeCompendiumPickers.MonsterSelected(__instance, entry);
}
