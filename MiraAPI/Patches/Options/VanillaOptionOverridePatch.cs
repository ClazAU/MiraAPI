using System.Reflection;
using AmongUs.GameOptions;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Utilities;

namespace MiraAPI.Patches.Options;

[HarmonyPatch]
public static class VanillaOptionOverridePatch
{
    private static readonly System.Type? Options = PatchTargets.NewestVersion(typeof(IGameOptions), "NormalGameOptionsV");

    public static bool Prepare() => Options != null;

    // Every float read funnels through TryGetFloat: GetFloat calls it, and LogicOptions reads it directly.
    public static MethodBase TargetMethod() =>
        AccessTools.Method(Options, nameof(NormalGameOptionsV12.TryGetFloat), [typeof(FloatOptionNames), typeof(float).MakeByRefType()]);

    [HarmonyPostfix]
    public static void Postfix(FloatOptionNames optionName, ref float value, ref bool __result)
    {
        if (VanillaOptionOverrides.TryGetFloat(optionName, out var overridden))
        {
            value = overridden;
            __result = true;
        }
    }
}
