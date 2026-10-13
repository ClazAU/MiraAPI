using System.Linq;
using AmongUs.GameOptions;
using MiraAPI.Networking;
using Reactor.Networking.Attributes;

namespace MiraAPI.Roles;

/// <summary>
/// Hands out custom ghost roles (a role whose <see cref="RoleBehaviour.IsDead"/> is true) when a player dies, from the
/// role's lobby count and chance, the way vanilla hands out Guardian Angel and Spirit Guide. Only ghost roles with
/// <see cref="CustomRoleConfiguration.HideSettings"/> turned off take part; ghost roles are hidden by default because
/// most are reached through another role's <see cref="CustomRoleConfiguration.GhostRole"/> instead. Freeplay assigns
/// any ghost role whose count is above zero, so enabling one in the lobby is enough to test it.
/// </summary>
internal static class GhostRoleAssignment
{
    public static RoleTypes? Roll(bool impostor)
    {
        var roleOptions = GameOptionsManager.Instance.CurrentGameOptions.RoleOptions;
        var freeplay = AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay;

        var candidates = CustomRoleManager.CustomRoleBehaviours
            .Where(role => role.IsDead && role.IsImpostor == impostor && CustomRoleUtils.CanSpawnOnCurrentMode(role))
            .Select(role => role.Role)
            .OrderBy(_ => HashRandom.Next(int.MaxValue));

        foreach (var role in candidates)
        {
            var maxCount = roleOptions.GetNumPerGame(role);
            if (maxCount <= 0)
            {
                continue;
            }

            if (freeplay)
            {
                return role;
            }

            var held = PlayerControl.AllPlayerControls.ToArray()
                .Count(player => player != null && player.Data != null && player.Data.Role != null && player.Data.Role.Role == role);
            if (held < maxCount && HashRandom.Next(101) < roleOptions.GetChancePerGame(role))
            {
                return role;
            }
        }

        return null;
    }

    // What vanilla's CoSetRole does for a ghost role, run on every client from the host's decision.
    [MethodRpc((uint)MiraRpc.SetGhostRole)]
    public static void RpcSetGhostRole(PlayerControl sender, PlayerControl target, ushort roleId)
    {
        if (sender == null || AmongUsClient.Instance == null || sender.OwnerId != AmongUsClient.Instance.HostId)
        {
            return;
        }

        if (target == null || target.Data == null || target.Data.Disconnected)
        {
            return;
        }

        var role = (RoleTypes)roleId;
        if (!RoleManager.InstanceExists || !RoleManager.IsGhostRole(role))
        {
            return;
        }

        RoleManager.Instance.SetRole(target, role);
        target.Data.Role.SpawnTaskHeader(target);
        if (target == PlayerControl.LocalPlayer && HudManager.InstanceExists)
        {
            HudManager.Instance.ReportButton.gameObject.SetActive(false);
        }
    }
}
