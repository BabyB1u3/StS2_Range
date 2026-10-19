using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;

internal static class PauseMenuSmoke
{
    public static void Run(Assembly mod, Harmony harmony)
    {
        foreach (var type in mod.GetTypes().Where(t => t.Name.StartsWith("RangePause", StringComparison.Ordinal)
                     && t.Name.EndsWith("Patch", StringComparison.Ordinal)))
            harmony.CreateClassProcessor(type).Patch();

        object? Invoke(string type, string method, params object?[] args) =>
            AccessTools.Method(mod.GetType("SpireRange." + type)!, method).Invoke(null, args);
        var session = mod.GetType("SpireRange.RangeSession")!;
        var active = AccessTools.Field(session, "<IsActive>k__BackingField");
        var busy = AccessTools.Field(session, "<IsBusy>k__BackingField");
        var oldActive = active.GetValue(null);
        var oldBusy = busy.GetValue(null);
        try
        {
            active.SetValue(null, false);
            var menu = (NPauseMenu)RuntimeHelpers.GetUninitializedObject(typeof(NPauseMenu));
            Invoke("RangePauseMenu", "Apply", menu);
            if (Invoke("RangePauseResetPatch", "Prefix") is not true
                || Invoke("RangePauseGiveUpPatch", "Prefix") is not true
                || Invoke("RangePauseExitPatch", "Prefix") is not true)
                throw new Exception("Normal pause menu behavior was intercepted.");

            active.SetValue(null, true);
            busy.SetValue(null, true);
            // Stale clicks must skip the native disconnect/save-and-quit actions,
            // while the busy guard prevents starting a second reset or teardown.
            if (Invoke("RangePauseResetPatch", "Prefix") is not false
                || Invoke("RangePauseGiveUpPatch", "Prefix") is not false
                || Invoke("RangePauseExitPatch", "Prefix") is not false
                || busy.GetValue(null) is not true)
                throw new Exception("Busy practice actions fell through to native navigation.");
            Console.WriteLine("PASS: pause menu patches bind, normal actions pass through, and busy practice clicks are ignored.");
        }
        finally
        {
            active.SetValue(null, oldActive);
            busy.SetValue(null, oldBusy);
        }
    }
}
