using AmongUs.GameOptions;

namespace MiraAPI.Events.Vanilla.Player;

/// <summary>
/// Invoked on the host when a dead player is about to get a ghost role, before MiraAPI rolls custom ghost roles and
/// vanilla rolls Guardian Angel or Spirit Guide. Set <see cref="GhostRole"/> to choose it yourself: MiraAPI then gives
/// it on every client and skips both rolls. Not raised for roles whose configuration names a fixed ghost role, or in
/// modes without special ghost roles.
/// </summary>
/// <param name="player">The player who died.</param>
public sealed class ChooseGhostRoleEvent(PlayerControl player) : MiraEvent
{
    /// <summary>
    /// Gets the player who died.
    /// </summary>
    public PlayerControl Player { get; } = player;

    /// <summary>
    /// Gets or sets the ghost role to give, or <see langword="null"/> to let the usual rolls decide.
    /// </summary>
    public RoleTypes? GhostRole { get; set; }
}
