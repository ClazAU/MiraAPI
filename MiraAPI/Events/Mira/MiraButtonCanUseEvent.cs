using MiraAPI.Hud;

namespace MiraAPI.Events.Mira;

/// <summary>
/// Invoked locally whenever a custom button checks whether it can be used: every tick to light it up, and again on a
/// click or keybind press. Cancel it to keep the button unusable, for example to enforce a player-wide "can't use
/// abilities" state from a library. The same instance is reused for a button between checks.
/// </summary>
/// <param name="button">The button being checked.</param>
public sealed class MiraButtonCanUseEvent(CustomActionButton button) : MiraCancelableEvent
{
    /// <summary>
    /// Gets the button being checked.
    /// </summary>
    public CustomActionButton Button { get; } = button;
}
