using System;
using System.Collections.Generic;
using AmongUs.GameOptions;

namespace MiraAPI.GameOptions;

/// <summary>
/// Overrides vanilla float options, such as the kill cooldown, wherever the game reads them on this client, including
/// reads that skip <c>GetFloat</c> (the kill timer after a meeting reads <c>TryGetFloat</c> directly). Normal mode only.
/// Nothing here is networked: compute the value from state every client shares, such as synced options and roles.
/// </summary>
public static class VanillaOptionOverrides
{
    private static readonly List<Func<FloatOptionNames, float?>> FloatSources = [];

    /// <summary>
    /// Adds a source of float overrides. It is asked on every float option read; return <see langword="null"/> to leave
    /// the option alone. When several sources return a value, the one added last wins.
    /// </summary>
    /// <param name="source">The override source.</param>
    public static void AddFloatSource(Func<FloatOptionNames, float?> source) => FloatSources.Add(source);

    /// <summary>
    /// Removes a source added with <see cref="AddFloatSource"/>.
    /// </summary>
    /// <param name="source">The override source.</param>
    public static void RemoveFloatSource(Func<FloatOptionNames, float?> source) => FloatSources.Remove(source);

    internal static bool TryGetFloat(FloatOptionNames option, out float value)
    {
        for (var i = FloatSources.Count - 1; i >= 0; i--)
        {
            float? overridden;
            try
            {
                overridden = FloatSources[i](option);
            }
            catch (Exception e)
            {
                Error($"Float option override for {option} threw: {e}");
                continue;
            }

            if (overridden is { } result)
            {
                value = result;
                return true;
            }
        }

        value = 0f;
        return false;
    }
}
