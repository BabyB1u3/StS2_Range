using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace SpireRange;

internal static class RangeCharacterPicker
{
    private sealed record Picker(NMainMenu Menu, RangeSetupScreen? Owner, Action Unsubscribe);

    // The native screen is cached by the submenu stack and also used by ordinary runs.
    // Register the specific visit, and remove it on both Back and confirmation.
    private static readonly Dictionary<NCharacterSelectScreen, Picker> Pickers = new(ReferenceEqualityComparer.Instance);

    public static bool IsPicker(NCharacterSelectScreen screen) => Pickers.ContainsKey(screen);

    public static void Open(NMainMenu menu, RangeSetupScreen? owner = null)
    {
        var stack = menu.SubmenuStack;
        var screen = stack.GetSubmenuType<NCharacterSelectScreen>();
        if (IsPicker(screen)) return;
        void Cleanup() => Closed(screen);
        Pickers.Add(screen, new Picker(menu, owner, () => screen.TreeExiting -= Cleanup));
        screen.TreeExiting += Cleanup;
        try
        {
            screen.InitializeSingleplayer();
            stack.Push(screen);
        }
        catch
        {
            Closed(screen);
            throw;
        }
    }

    public static void Opened(NCharacterSelectScreen screen)
    {
        if (!Pickers.TryGetValue(screen, out var picker)) return;
        if (picker.Owner is { } owner)
        {
            var buttons = NativeAccess.Get<Control>(screen, "_charButtonContainer");
            buttons.GetChildren().OfType<NCharacterSelectButton>()
                .FirstOrDefault(b => b.Character.Id == owner.SelectedCharacter.Id && !b.IsLocked)?.Select();
        }
        HideAscension(screen);
    }

    public static void HideAscension(NCharacterSelectScreen screen)
    {
        if (!IsPicker(screen)) return;
        // Practice still uses its existing ascension-zero configuration.
        NativeAccess.Get<Control>(screen, "_ascensionPanel").Visible = false;
    }

    public static void Closed(NCharacterSelectScreen screen)
    {
        if (Pickers.Remove(screen, out var picker)) picker.Unsubscribe();
    }

    // Return false only for our picker. Do not call SetReady: that starts a normal
    // run lobby, updates preferred ascension, and may show the tutorial prompt.
    public static bool Confirm(NCharacterSelectScreen screen)
    {
        if (!Pickers.TryGetValue(screen, out var picker)) return true;
        var stack = picker.Menu.SubmenuStack;
        if (!ReferenceEquals(stack.Peek(), screen)) return false;
        var selected = NativeAccess.Get<NCharacterSelectButton?>(screen, "_selectedButton");
        if (selected is null || selected.IsLocked) return false;
        var character = ResolveCharacter(selected.Character);
        // Native Pop invokes OnSubmenuClosed, which cleans up the singleplayer lobby.
        stack.Pop();
        if (picker.Owner is { } owner)
            owner.ChooseCharacter(character);
        else
        {
            var setup = RangeSetupScreen.Create(picker.Menu, character);
            stack.AddChild(setup);
            stack.Push(setup);
        }
        return false;
    }

    internal static CharacterModel ResolveCharacter(CharacterModel character) =>
        character is RandomCharacter
            ? ModelDb.AllCharacters.ElementAt(Random.Shared.Next(ModelDb.AllCharacters.Count()))
            : character;
}

[HarmonyPatch(typeof(NCharacterSelectScreen), "OnEmbarkPressed")]
internal static class RangeCharacterConfirmPatch
{
    private static bool Prefix(NCharacterSelectScreen __instance) => RangeCharacterPicker.Confirm(__instance);
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.OnSubmenuOpened))]
internal static class RangeCharacterOpenedPatch
{
    private static void Postfix(NCharacterSelectScreen __instance) => RangeCharacterPicker.Opened(__instance);
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.OnSubmenuClosed))]
internal static class RangeCharacterClosedPatch
{
    private static void Postfix(NCharacterSelectScreen __instance) => RangeCharacterPicker.Closed(__instance);
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
internal static class RangeCharacterSelectedPatch
{
    private static void Postfix(NCharacterSelectScreen __instance) => RangeCharacterPicker.HideAscension(__instance);
}
