using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;

namespace MiraAPI.Utilities;

/// <summary>
/// Picks Harmony targets across a vanilla class family. Patching only a base virtual misses every override, and
/// patching each override blindly can detour one native function twice: MSVC folds identical method bodies into one
/// address, and two detours on one address break each other.
/// </summary>
public static class PatchTargets
{
    /// <summary>
    /// Gets <paramref name="name"/> as declared on <paramref name="root"/> and on every subclass of it in the same
    /// assembly, skipping abstract declarations and any whose native code another target already shares.
    /// Use it from a <c>[HarmonyPatch]</c> class's <c>TargetMethods()</c>.
    /// </summary>
    /// <param name="root">The base class, such as <see cref="Minigame"/>.</param>
    /// <param name="name">The method name.</param>
    /// <param name="parameters">The method's parameter types.</param>
    /// <returns>One method per native address.</returns>
    public static IEnumerable<MethodBase> DeclaredOverrides(Type root, string name, params Type[] parameters)
    {
        var seen = new HashSet<IntPtr>();
        var targets = new List<MethodBase>();
        var folded = 0;

        foreach (var type in root.Assembly.GetTypes())
        {
            if (type != root && !type.IsSubclassOf(root)) continue;

            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, parameters, null);
            if (method == null || method.IsAbstract) continue;

            var address = NativeAddress(method);
            if (address != IntPtr.Zero && !seen.Add(address))
            {
                folded++;
                continue;
            }

            targets.Add(method);
        }

        Info($"{root.Name}.{name}: {targets.Count} patch targets, {folded} folded duplicates skipped");
        return targets;
    }

    private static IntPtr NativeAddress(MethodBase method)
    {
        var field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
        if (field?.GetValue(null) is not IntPtr methodInfo || methodInfo == IntPtr.Zero) return IntPtr.Zero;
        return Marshal.ReadIntPtr(methodInfo);
    }
}
