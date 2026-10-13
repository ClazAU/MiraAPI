using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Roles;

namespace MiraAPI.Patches.Roles;

/// <summary>
/// Patches for custom ejection messages.
/// </summary>
[HarmonyPatch(typeof(ExileController))]
internal static class EjectionPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(ExileController.Begin))]
    public static void BeginPostfix(ExileController __instance)
    {
        var exiled = __instance.initData.networkedPlayer;
        if (exiled && exiled.Role && exiled.Role is ICustomRole role &&
            GameManager.Instance.LogicOptions.GetConfirmImpostor() &&
            role.GetCustomEjectionMessage(exiled) is { } message)
        {
            __instance.completeString = message;
        }

        // Raised after the custom message is written, so text a handler adds to completeString stays.
        MiraEventManager.InvokeEvent(new EjectionEvent(__instance));
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.ReEnableGameplay))]
    public static class ReEnableGameplayPatch
    {
        // Vanilla destroys the controller only after gameplay is back and never clears Instance, so the HUD re-show, button
        // Enabled checks, CanMove and every round-start handler would all still see an ejection in progress.
        [HarmonyPrefix]
        public static void Prefix(ExileController __instance)
        {
            if (ExileController.Instance == __instance)
            {
                ExileController.Instance = null;
            }
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            var @event = new BeforeRoundStartEvent(false);
            MiraEventManager.InvokeEvent(@event);

            if (@event.IsCancelled) return;

            var @event2 = new RoundStartEvent(false);
            MiraEventManager.InvokeEvent(@event2);
        }
    }
}
