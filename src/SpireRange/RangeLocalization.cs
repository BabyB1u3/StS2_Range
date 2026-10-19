using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace SpireRange;

internal static class RangeLocalization
{
    public static void Register()
    {
        var manager = LocManager.Instance;
        if (manager is null) return;
        var id = ModelDb.GetId<RangeEncounter>().Entry;
        manager.GetTable("encounters").MergeWith(Entries(id, manager.Language));
    }

    internal static Dictionary<string, string> Entries(string id, string language)
    {
        var chinese = language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        return new Dictionary<string, string>
        {
            [id + ".title"] = chinese ? "靶场练习" : "Practice Range",
            [id + ".loss"] = chinese
                ? "本次靶场练习已结束。可以重置战斗，或返回主菜单重新选择。"
                : "Practice complete. Reset the battle, or return to the main menu to choose another setup."
        };
    }
}

[HarmonyPatch(typeof(LocManager), nameof(LocManager.SetLanguage))]
internal static class RangeLanguagePatch
{
    private static void Postfix() => RangeLocalization.Register();
}
