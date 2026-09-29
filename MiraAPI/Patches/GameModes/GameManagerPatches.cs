using HarmonyLib;
using MiraAPI.GameModes;

namespace MiraAPI.Patches.GameModes;

[HarmonyPatch(typeof(GameManager))]
internal static class GameManagerPatches
{
    [HarmonyPrefix, HarmonyPatch(nameof(GameManager.OnPlayerDeath))]
    public static bool OnDeathPrefix([HarmonyArgument(0)] PlayerControl player, [HarmonyArgument(1)] bool assignGhostRole)
    {
        if (CustomGameModeManager.ActiveMode != null)
        {
            CustomGameModeManager.ActiveMode.OnPlayerDeath(player, assignGhostRole);
            return false;
        }

        return true;
    }
    [HarmonyPrefix, HarmonyPatch(nameof(GameManager.ShowCrewmatesKilled))]
    public static bool ShowCrewmatesKilledPrefix(GameManager __instance, ref bool __result)
    {
        // On 2026.9.29 x64 this `return false` body is folded with other zero-returning methods (an Addressables
        // DebugName getter among them), so only answer for the real game manager.
        if (CustomGameModeManager.ActiveMode != null && __instance == GameManager.Instance)
        {
            __result = CustomGameModeManager.ActiveMode is HideAndSeekMode;
            return false;
        }

        return true;
    }
    [HarmonyPatch(typeof(NormalGameManager), nameof(NormalGameManager.GetMapOptions))]
    [HarmonyPrefix]
    public static bool GetMapOptions(ref MapOptions __result)
    {
        if (CustomGameModeManager.ActiveMode == null)
        {
            return true;
        }

        __result = CustomGameModeManager.ActiveMode.GetMapOptions();
        return false;
    }
}

[HarmonyPatch(typeof(GameStartManager))]
internal static class GameStartManagerPatches
{
    [HarmonyPrefix, HarmonyPatch(nameof(GameStartManager.Start))]
    public static void StartPatch(GameStartManager __instance)
    {
        if (MiraApiPlugin.IsDevBuild)
        {
            __instance.MinPlayers = 1;
        }
    }
}