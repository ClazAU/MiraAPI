using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
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

    /// <summary>
    /// Gets the newest of a versioned family of vanilla types, such as <c>NormalGameOptionsV12</c>. Each game options
    /// version adds a <c>V{N}</c> class and keeps the older ones around unused, so a patch must find the newest in the
    /// running game rather than name one at compile time.
    /// </summary>
    /// <param name="sibling">Any type in the same assembly and namespace, such as the family's interface.</param>
    /// <param name="prefix">The name before the version number, such as <c>"NormalGameOptionsV"</c>.</param>
    /// <returns>The type with the highest version, or <see langword="null"/> if there is none.</returns>
    public static Type? NewestVersion(Type sibling, string prefix) =>
        AccessTools.GetTypesFromAssembly(sibling.Assembly)
            .Select(type => (Type: type, Version: Version(type, sibling.Namespace, prefix)))
            .Where(x => x.Version.HasValue)
            .MaxBy(x => x.Version)
            .Type;

    private static int? Version(Type type, string? ns, string prefix) =>
        type.Namespace == ns &&
        type.Name.StartsWith(prefix, StringComparison.Ordinal) &&
        int.TryParse(type.Name.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? version
            : null;

    private static IntPtr NativeAddress(MethodBase method)
    {
        var field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
        if (field?.GetValue(null) is not IntPtr methodInfo || methodInfo == IntPtr.Zero) return IntPtr.Zero;
        return Marshal.ReadIntPtr(methodInfo);
    }
}
