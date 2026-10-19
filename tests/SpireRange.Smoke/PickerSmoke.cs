using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

internal static class PickerSmoke
{
    public static void Run(Assembly assembly, Harmony harmony)
    {
        // Validate every picker patch against this installed build, including the
        // compiler-generated native search closure. No Godot nodes are constructed.
        foreach (var type in assembly.GetTypes().Where(t => t.Name.StartsWith("RangeCard", StringComparison.Ordinal)
                     || t.Name.StartsWith("RangeBestiary", StringComparison.Ordinal)))
            harmony.CreateClassProcessor(type).Patch();

        var library = (NCardLibrary)RuntimeHelpers.GetUninitializedObject(typeof(NCardLibrary));
        var bestiary = (NBestiary)RuntimeHelpers.GetUninitializedObject(typeof(NBestiary));
        object? Invoke(string type, string method, params object?[] args) =>
            AccessTools.Method(assembly.GetType("SpireRange." + type)!, method).Invoke(null, args);

        if (Invoke("RangeCardPickPatch", "Prefix", library, null) is not true
            || Invoke("RangeBestiaryEntriesPatch", "Prefix", bestiary) is not true
            || Invoke("RangeCardDisplayPatch", "Prefix", library, null) is not true)
            throw new Exception("An original Compendium page was intercepted.");

        var id = new ModelId("CARD", "TEST_CARD");
        var discovered = new HashSet<ModelId>();
        if (Invoke("RangeCardSearchPatch", "IsDiscovered", discovered, id, library) is not false)
            throw new Exception("Normal card discovery was bypassed.");
        discovered.Add(id);
        if (Invoke("RangeCardSearchPatch", "IsDiscovered", discovered, id, library) is not true)
            throw new Exception("Normal discovered cards were hidden.");

        var registry = (IDictionary)AccessTools.Field(assembly.GetType("SpireRange.RangeCompendiumPickers")!, "Pickers")
            .GetValue(null)!;
        var pickerType = assembly.GetType("SpireRange.RangeCompendiumPickers+Picker")!;
        registry.Add(library, RuntimeHelpers.GetUninitializedObject(pickerType));
        try
        {
            discovered.Clear();
            if (Invoke("RangeCardSearchPatch", "IsDiscovered", discovered, id, library) is not true
                || discovered.Count != 0)
                throw new Exception("Picker discovery scope mutated progress or failed to expose test cards.");
        }
        finally { registry.Remove(library); }
        if (Invoke("RangeCardSearchPatch", "IsDiscovered", discovered, id, library) is not false)
            throw new Exception("Picker discovery scope leaked after cleanup.");
        Console.WriteLine("PASS: native picker patches bind; original Compendium pages and discovery remain unchanged.");
    }
}
