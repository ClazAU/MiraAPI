using System;
using MiraAPI.PluginLoading;
using UnityEngine;

namespace MiraAPI.Events.Vanilla.Minigames;

/// <summary>
/// Shared shape of <see cref="BeforeMinigameOpenEvent"/> and <see cref="MinigameOpenEvent"/>: the panel being opened,
/// plus a claim a mod takes when it destroys or heavily modifies the panel, so other mods leave it alone.
/// A claim taken in <see cref="BeforeMinigameOpenEvent"/> carries into <see cref="MinigameOpenEvent"/> for the same panel.
/// </summary>
/// <param name="gameObject">The minigame's <see cref="UnityEngine.GameObject"/>.</param>
/// <param name="modifiedBy">The owner of an existing claim, if any.</param>
[MiraIgnore]
public abstract class MinigameOpenEventBase(GameObject gameObject, string? modifiedBy) : MiraEvent
{
    /// <summary>
    /// Gets the minigame's <see cref="UnityEngine.GameObject"/>.
    /// </summary>
    public GameObject GameObject { get; } = gameObject;

    /// <summary>
    /// Gets the id of the mod that claimed this panel, or <see langword="null"/> if none has.
    /// </summary>
    public string? ModifiedBy { get; private set; } = modifiedBy;

    /// <summary>
    /// Gets a value indicating whether a mod has claimed this panel.
    /// </summary>
    public bool IsModified => ModifiedBy != null;

    /// <summary>
    /// Claims the panel for <paramref name="owner"/> unless another mod already has.
    /// </summary>
    /// <param name="owner">The claiming mod's id, such as its plugin id.</param>
    /// <returns><see langword="true"/> if the panel is now, or already was, claimed by <paramref name="owner"/>.</returns>
    public bool TryMarkModified(string owner)
    {
        if (string.IsNullOrEmpty(owner)) throw new ArgumentException("A claim needs an owner.", nameof(owner));

        ModifiedBy ??= owner;
        return ModifiedBy == owner;
    }
}
