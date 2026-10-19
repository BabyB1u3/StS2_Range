using MegaCrit.Sts2.Core.Helpers;

namespace SpireRange;

internal static class RangeSeed
{
    public static string Resolve(string? input) => string.IsNullOrWhiteSpace(input)
        ? SeedHelper.GetRandomSeed()
        : SeedHelper.CanonicalizeSeed(input);
}
