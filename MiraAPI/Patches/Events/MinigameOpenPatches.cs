using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Common;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Minigames;

namespace MiraAPI.Patches.Events;

[HarmonyPatch]
public static class MinigameOpenPatches
{
    private static readonly List<IntPtr> Opening = [];

    // Every override, not just the base virtual: overrides that never call base.Begin would be missed.
    public static IEnumerable<MethodBase> TargetMethods()
    {
        var root = typeof(Minigame);
        var seenNative = new HashSet<IntPtr>();
        var skipped = 0;

        foreach (var type in root.Assembly.GetTypes())
        {
            if (type != root && !type.IsSubclassOf(root)) continue;

            var begin = type.GetMethod(
                nameof(Minigame.Begin),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                null,
                [typeof(PlayerTask)],
                null);
            if (begin == null || begin.IsAbstract) continue;

            // MSVC folds identical bodies, and two detours on one native address break each other.
            var address = NativeAddress(begin);
            if (address != IntPtr.Zero && !seenNative.Add(address))
            {
                skipped++;
                continue;
            }

            yield return begin;
        }

        Info($"Minigame open events: patched Begin on {seenNative.Count} native addresses, skipped {skipped} folded duplicates");
    }

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

    private static IntPtr NativeAddress(MethodBase method)
    {
        var field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
        if (field?.GetValue(null) is not IntPtr methodInfo || methodInfo == IntPtr.Zero) return IntPtr.Zero;
        return Marshal.ReadIntPtr(methodInfo);
    }
}
