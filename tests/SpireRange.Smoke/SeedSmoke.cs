using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

internal static class SeedSmoke
{
    public static void Run(Assembly mod)
    {
        var resolve = AccessTools.Method(mod.GetType("SpireRange.RangeSeed"), "Resolve");
        string Resolve(string input) => (string)resolve.Invoke(null, new object[] { input })!;
        int[] ShuffleStream(string seed)
        {
            var rng = new RunRngSet(seed).Shuffle;
            return Enumerable.Range(0, 12).Select(_ => rng.NextInt()).ToArray();
        }
        var seeds = Enumerable.Range(0, 16).Select(_ => Resolve("")).ToArray();
        if (seeds.Distinct().Count() <= 1 || seeds.Select(s => string.Join(',', ShuffleStream(s))).Distinct().Count() <= 1)
            throw new Exception("Blank input still produces a fixed seed or shuffle stream.");
        var selected = seeds[0];
        if (Resolve(selected) != selected || !ShuffleStream(selected).SequenceEqual(ShuffleStream(Resolve(selected))))
            throw new Exception("Resetting a resolved seed changes the native shuffle stream.");
        if (Resolve(" test123 ") != Resolve("TEST123"))
            throw new Exception("Explicit seeds are not normalized consistently.");
        Console.WriteLine("PASS: new random seeds vary the native shuffle RNG; explicit/resolved seeds reproduce it.");
    }
}
