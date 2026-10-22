using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

internal static class CharacterPickerSmoke
{
    public static void Run(Assembly mod, Harmony harmony)
    {
        foreach (var type in mod.GetTypes().Where(t => t.Name.StartsWith("RangeCharacter", StringComparison.Ordinal)
                     && t.Name.EndsWith("Patch", StringComparison.Ordinal)))
            harmony.CreateClassProcessor(type).Patch();

        object? Invoke(string type, string method, params object?[] args) =>
            AccessTools.Method(mod.GetType("SpireRange." + type)!, method).Invoke(null, args);
        var screen = (NCharacterSelectScreen)RuntimeHelpers.GetUninitializedObject(typeof(NCharacterSelectScreen));
        var other = (NCharacterSelectScreen)RuntimeHelpers.GetUninitializedObject(typeof(NCharacterSelectScreen));
        var pickerType = mod.GetType("SpireRange.RangeCharacterPicker+Picker")!;
        var registry = (IDictionary)AccessTools.Field(mod.GetType("SpireRange.RangeCharacterPicker"), "Pickers").GetValue(null)!;
        // None of these unregistered callbacks may access Godot nodes or a lobby.
        // Exercise the actual patched lifecycle methods too: a native page remains usable
        // after a previous picker visit has been closed or cancelled.
        for (var visit = 0; visit < 3; visit++)
        {
            var unsubscribed = 0;
            var picker = Activator.CreateInstance(pickerType, new object?[] { null, null, (Action)(() => unsubscribed++) })!;
            registry.Add(screen, picker);
            try
            {
                if (Invoke("RangeCharacterPicker", "IsPicker", screen) is not true
                    || Invoke("RangeCharacterConfirmPatch", "Prefix", other) is not true)
                    throw new Exception("Character picker interception leaked across screen instances.");
                Invoke("RangeCharacterClosedPatch", "Postfix", screen);
                Invoke("RangeCharacterClosedPatch", "Postfix", screen);
                if (unsubscribed != 1 || registry.Count != 0)
                    throw new Exception("Closing the picker left a stale registration or signal subscription.");
            }
            finally { registry.Remove(screen); }
            Invoke("RangeCharacterOpenedPatch", "Postfix", screen);
            Invoke("RangeCharacterSelectedPatch", "Postfix", screen);
            if (Invoke("RangeCharacterConfirmPatch", "Prefix", screen) is not true)
                throw new Exception("Ordinary character selection was intercepted.");
        }

        ModelDb.Inject(typeof(Ironclad));
        ModelDb.Inject(typeof(Silent));
        ModelDb.Inject(typeof(Regent));
        ModelDb.Inject(typeof(Necrobinder));
        ModelDb.Inject(typeof(Defect));
        ModelDb.Inject(typeof(RandomCharacter));
        var ironclad = ModelDb.Character<Ironclad>();
        if (!ReferenceEquals(Invoke("RangeCharacterPicker", "ResolveCharacter", ironclad), ironclad))
            throw new Exception("An explicitly selected character was replaced.");
        for (var i = 0; i < 20; i++)
        {
            var resolved = (CharacterModel)Invoke("RangeCharacterPicker", "ResolveCharacter", ModelDb.Character<RandomCharacter>())!;
            if (resolved is RandomCharacter || !ModelDb.AllCharacters.Contains(resolved))
                throw new Exception("Random selection did not resolve to a playable character.");
        }
        Console.WriteLine("PASS: native character patches bind; picker scope clears across repeated visits; ordinary screens pass through; explicit and random choices resolve to playable models.");
    }
}
