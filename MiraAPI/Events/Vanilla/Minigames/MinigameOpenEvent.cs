using UnityEngine;

namespace MiraAPI.Events.Vanilla.Minigames;

/// <summary>
/// Invoked locally when a <see cref="Minigame"/> opens, after its <see cref="Minigame.Begin"/> returns.
/// Fires for every minigame, task or not, once per instance, including stages a <see cref="MultistageMinigame"/> opens.
/// Minigames that set themselves up in <c>Start</c> (such as <see cref="AirshipUploadGame"/>) have not run it yet.
/// </summary>
/// <param name="gameObject">The minigame's <see cref="UnityEngine.GameObject"/>.</param>
/// <param name="modifiedBy">The owner of the claim taken in <see cref="BeforeMinigameOpenEvent"/>, if any.</param>
public sealed class MinigameOpenEvent(GameObject gameObject, string? modifiedBy = null) : MinigameOpenEventBase(gameObject, modifiedBy);
