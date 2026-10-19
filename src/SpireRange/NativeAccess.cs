using HarmonyLib;

namespace SpireRange;

internal static class NativeAccess
{
    public static T Get<T>(object instance, string field) =>
        (T)AccessTools.Field(instance.GetType(), field).GetValue(instance)!;

    public static void Set(object instance, string field, object? value) =>
        AccessTools.Field(instance.GetType(), field).SetValue(instance, value);

    public static object? Call(object instance, string method, params object?[] args) =>
        AccessTools.Method(instance.GetType(), method).Invoke(instance, args);
}
