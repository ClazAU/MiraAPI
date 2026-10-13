using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace MiraAPI.Utilities;

/// <summary>
/// Writes IL2CPP reference arrays through the GC write barrier. Il2CppInterop's element setter, and so every
/// <c>T[]</c> conversion, stores raw pointers; the game's incremental GC can then free an element while the array
/// still points at it, and a later collection crashes in the GC mark loop. Fill or grow ship arrays (AllConsoles,
/// task pools, TaskSet lists, cosmetics) through these helpers.
/// </summary>
public static class Il2CppArrays
{
    private static readonly int HeaderSize = 4 * IntPtr.Size;

    /// <summary>
    /// Stores <paramref name="value"/> at <paramref name="index"/> through the GC write barrier.
    /// </summary>
    /// <param name="array">The array to write into.</param>
    /// <param name="index">The element index.</param>
    /// <param name="value">The value to store; <see langword="null"/> clears the slot.</param>
    /// <typeparam name="T">The element type.</typeparam>
    public static void Set<T>(Il2CppReferenceArray<T> array, int index, T? value)
        where T : Il2CppObjectBase
    {
        if ((uint)index >= (uint)array.Length) throw new ArgumentOutOfRangeException(nameof(index));

        var slot = array.Pointer + HeaderSize + (index * IntPtr.Size);
        IL2CPP.il2cpp_gc_wbarrier_set_field(array.Pointer, slot, value?.Pointer ?? IntPtr.Zero);
    }

    /// <summary>
    /// Creates a new array holding <paramref name="items"/>.
    /// </summary>
    /// <param name="items">The elements.</param>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>The new array.</returns>
    public static Il2CppReferenceArray<T> Of<T>(params T[] items)
        where T : Il2CppObjectBase
    {
        var result = new Il2CppReferenceArray<T>(items.Length);
        for (var i = 0; i < items.Length; i++) Set(result, i, items[i]);
        return result;
    }

    /// <summary>
    /// Creates a copy of <paramref name="source"/> with <paramref name="extra"/> added at the end.
    /// </summary>
    /// <param name="source">The array to copy; <see langword="null"/> reads as empty.</param>
    /// <param name="extra">The element to append.</param>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>The new array.</returns>
    public static Il2CppReferenceArray<T> Append<T>(Il2CppReferenceArray<T>? source, T extra)
        where T : Il2CppObjectBase
    {
        var length = source?.Length ?? 0;
        var result = new Il2CppReferenceArray<T>(length + 1);
        for (var i = 0; i < length; i++) Set(result, i, source![i]);
        Set(result, length, extra);
        return result;
    }
}
