using UnityEngine;

namespace MiraAPI.Events.Vanilla.Minigames;

/// <summary>
/// Invoked locally when a <see cref="Minigame"/> opens, before its <see cref="Minigame.Begin"/> runs.
/// Fires for every minigame, task or not, once per instance, including stages a <see cref="MultistageMinigame"/> opens.
/// Change serialized fields <see cref="Minigame.Begin"/> reads here; the task is not attached yet.
/// </summary>
/// <param name="gameObject">The minigame's <see cref="UnityEngine.GameObject"/>.</param>
public sealed class BeforeMinigameOpenEvent(GameObject gameObject) : MinigameOpenEventBase(gameObject, null);
