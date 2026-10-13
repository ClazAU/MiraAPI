using Il2CppInterop.Common.Attributes;

namespace MiraAPI.Roles;

/// <summary>
/// Enum for modded role teams.
/// </summary>
// Every role's injected Team getter exposes this enum to IL2CPP. Without a name in an image IL2CPP knows, Il2CppInterop
// looks it up in "MiraAPI.dll" and logs that the assembly is not registered; the lookup comes back empty either way.
[OriginalName("Assembly-CSharp.dll", "MiraAPI.Roles", nameof(ModdedRoleTeams))]
public enum ModdedRoleTeams
{
    /// <summary>
    /// The crewmate team.
    /// </summary>
    Crewmate,

    /// <summary>
    /// The impostor team.
    /// </summary>
    Impostor,

    /// <summary>
    /// Custom team, catchall for any other teams.
    /// </summary>
    Custom,
}
