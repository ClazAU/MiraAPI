using MiraAPI.Hud;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace MiraAPI.SmokeTests;

public class SmokeRole : CrewmateRole, ICustomRole
{
    public const string EjectionMessage = "Smoke was the smoke test.";

    public string RoleName => "Smoke";

    public string RoleDescription => "Smoke test role";

    public string RoleLongDescription => "Smoke test role";

    public Color RoleColor => Color.gray;

    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;

    public CustomRoleConfiguration Configuration => new(this);

    public string? GetCustomEjectionMessage(NetworkedPlayerInfo player) => EjectionMessage;
}

public class SmokeGhostRole : CrewmateGhostRole, ICustomRole
{
    public string RoleName => "Smoke Ghost";

    public string RoleDescription => "Smoke test ghost role";

    public string RoleLongDescription => "Smoke test ghost role";

    public Color RoleColor => Color.gray;

    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;

    public CustomRoleConfiguration Configuration => new(this);
}

// Uses the full in-round guard CLAUDE.md recommends, ExileController included, which used to hide it after an ejection.
public class SmokeButton : CustomActionButton
{
    public static int Clicks { get; set; }

    public override string Name => "Smoke";

    public override float Cooldown => 0f;

    public override LoadableAsset<Sprite> Sprite => MiraAssets.Empty;

    public override bool Enabled(RoleBehaviour? role) =>
        role is SmokeRole &&
        ShipStatus.Instance != null &&
        MeetingHud.Instance == null &&
        ExileController.Instance == null &&
        PlayerControl.LocalPlayer?.Data is { IsDead: false };

    protected override void OnClick() => Clicks++;
}

public class SmokeModifier : BaseModifier
{
    public static bool? SawSelfOnActivate { get; set; }

    public static bool? SawSelfOnDeactivate { get; set; }

    public static int Activations { get; set; }

    public override string ModifierName => "Smoke";

    public override bool HideOnUi => true;

    public override void OnActivate()
    {
        Activations++;
        SawSelfOnActivate = Player.HasModifier<SmokeModifier>();
    }

    public override void OnDeactivate() => SawSelfOnDeactivate = Player.HasModifier<SmokeModifier>();
}
