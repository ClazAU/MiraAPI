using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Player;

namespace MiraAPI.Patches.Events;

[HarmonyPatch]
public static class LocalClientLeftPatches
{
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnDisconnected))]
    [HarmonyPostfix]
    public static void OnDisconnectedPostfix() => MiraEventManager.InvokeEvent(new LocalClientLeftEvent(false));

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
    [HarmonyPrefix]
    public static void ExitGamePrefix() => MiraEventManager.InvokeEvent(new LocalClientLeftEvent(true));
}
