using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Minigames;
using MiraAPI.Utilities;

namespace MiraAPI.Patches.Events;

[HarmonyPatch]
public static class MinigameOpenPatches
{
    private static readonly List<IntPtr> Opening = [];

    public static IEnumerable<MethodBase> TargetMethods() => PatchTargets.DeclaredOverrides(typeof(Minigame), nameof(Minigame.Begin), typeof(PlayerTask));

    [HarmonyPrefix]
    public static void Prefix(Minigame __instance, out BeforeMinigameOpenEvent? __state)
    {
        var pointer = __instance.Pointer;
        var outermost = !Opening.Contains(pointer);
        Opening.Add(pointer);

        __state = outermost ? new BeforeMinigameOpenEvent(__instance.gameObject) : null;
        if (__state != null)
        {
            MiraEventManager.InvokeEvent(__state);
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(Minigame __instance, Exception? __exception, BeforeMinigameOpenEvent? __state)
    {
        var index = Opening.LastIndexOf(__instance.Pointer);
        if (index >= 0) Opening.RemoveAt(index);

        if (__state != null && __exception == null && __instance)
        {
            MiraEventManager.InvokeEvent(new MinigameOpenEvent(__instance.gameObject, __state.ModifiedBy));
        }
    }
}
