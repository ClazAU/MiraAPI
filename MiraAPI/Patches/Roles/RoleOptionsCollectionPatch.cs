using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using MiraAPI.Roles;

namespace MiraAPI.Patches.Roles;

/// <summary>
/// Patches to return the correct role counts.
/// </summary>
public static class RoleOptionsCollectionPatch
{
    private const string CollectionPrefix = "RoleOptionsCollectionV";

    // Every game options version adds a RoleOptionsCollectionV{N} and keeps the older ones around unused
    // (2026.9.29 moved to V12), so patch whichever is newest in the running game, not one fixed at compile time.
    private static readonly Type? Collection = FindNewestCollection();

    private static Type? FindNewestCollection()
    {
        var collection = AccessTools.GetTypesFromAssembly(typeof(IRoleOptionsCollection).Assembly)
            .Select(type => (Type: type, Version: CollectionVersion(type)))
            .Where(x => x.Version.HasValue)
            .MaxBy(x => x.Version)
            .Type;

        if (collection == null)
        {
            Error("No RoleOptionsCollection type found, custom role counts will not apply.");
        }
        else
        {
            Info($"Patching role counts on {collection.Name}");
        }

        return collection;
    }

    private static int? CollectionVersion(Type type) =>
        type.Namespace == typeof(IRoleOptionsCollection).Namespace &&
        type.Name.StartsWith(CollectionPrefix, StringComparison.Ordinal) &&
        int.TryParse(type.Name.AsSpan(CollectionPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? version
            : null;

    // Attribute patches rather than harmony.Patch calls: the launcher's protector only leaves a patch method's
    // body unencrypted when it can see it is one, and an encrypted body can't run from the IL2CPP trampoline.
    [HarmonyPatch]
    internal static class AnyRolesEnabledPatch
    {
        public static bool Prepare() => Collection != null;

        public static MethodBase TargetMethod() => AccessTools.Method(Collection, nameof(RoleOptionsCollectionV11.AnyRolesEnabled));

        /// <summary>
        /// This patch fixes <see cref="RoleOptionsCollectionV11.GetNumPerGame(RoleTypes)"/> being inlined (2025.9.9) in the original code.
        /// </summary>
        public static bool Prefix(Il2CppObjectBase __instance, ref bool __result)
        {
            // Vanilla keys its roles dictionary by RoleTypes alone and counts a role it doesn't hold as zero, so walking
            // the enum gives the dictionary's answer without naming its per-version RoleData value type.
            var collection = __instance.Cast<IRoleOptionsCollection>();
            foreach (var role in Enum.GetValues<RoleTypes>())
            {
                if (collection.GetNumPerGame(role) > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [HarmonyPatch]
    internal static class GetChancePerGamePatch
    {
        public static bool Prepare() => Collection != null;

        public static MethodBase TargetMethod() => AccessTools.Method(Collection, nameof(IRoleOptionsCollection.GetChancePerGame));

        /// <summary>
        /// Set the role chance for custom Launchpad roles based on config.
        /// </summary>
        /// <returns>Return <see langword="false"/> to skip original method, <see langword="true"/> to not.</returns>
        public static bool Prefix(RoleTypes role, ref int __result)
        {
            if (!CustomRoleManager.GetCustomRoleBehaviour(role, out var customRole) || customRole == null)
            {
                return true;
            }

            if (customRole.Configuration.HideSettings)
            {
                __result = 0;
                return false;
            }

            var chance = customRole.GetChance();
            if (chance == null)
            {
                Error($"Chance is null, defaulting to zero.");
                chance = 0;
            }

            __result = chance.Value;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class GetNumPerGamePatch
    {
        public static bool Prepare() => Collection != null;

        public static MethodBase TargetMethod() => AccessTools.Method(Collection, nameof(IRoleOptionsCollection.GetNumPerGame));

        /// <summary>
        /// Set the amount for custom Launchpad roles based on config.
        /// </summary>
        /// <returns>Return <see langword="false"/> to skip original method, <see langword="true"/> to not.</returns>
        public static bool Prefix(RoleTypes role, ref int __result)
        {
            if (!CustomRoleManager.GetCustomRoleBehaviour(role, out var customRole) || customRole == null)
            {
                return true;
            }

            if (customRole.Configuration.HideSettings)
            {
                __result = 0;
                return false;
            }

            var count = customRole.GetCount();
            if (count == null)
            {
                Error($"Count is null, defaulting to zero.");
                count = 0;
            }

            __result = count.Value;
            return false;
        }
    }
}
