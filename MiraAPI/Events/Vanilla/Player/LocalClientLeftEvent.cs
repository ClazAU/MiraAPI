namespace MiraAPI.Events.Vanilla.Player;

/// <summary>
/// Invoked when the local client leaves the game: its connection dropped or closed (<c>AmongUsClient.OnDisconnected</c>),
/// or it quit Freeplay or a local game from the menu (<c>AmongUsClient.ExitGame</c>), which never reaches OnDisconnected.
/// Leaving an online game from the menu raises it twice, so handlers must be idempotent. From ExitGame it is raised before
/// the players are torn down, so a sweep still finds them. Non-cancelable.
/// </summary>
/// <param name="fromExitGame">Whether it came from <c>ExitGame</c> rather than a disconnect.</param>
public sealed class LocalClientLeftEvent(bool fromExitGame) : MiraEvent
{
    /// <summary>
    /// Gets a value indicating whether the client left through <c>AmongUsClient.ExitGame</c> rather than a disconnect.
    /// </summary>
    public bool FromExitGame { get; } = fromExitGame;
}
