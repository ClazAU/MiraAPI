using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace MiraAPI.Utilities;

/// <summary>
/// Builds IL2CPP reference arrays through the GC write barrier. Il2CppInterop's element setter, and so every
/// <c>T[]</c> conversion, stores raw pointers; the game's incremental GC can then free an element while the array
/// still points at it, and a later collection crashes in the GC mark loop.
/// </summary>
internal static class Il2CppArrays
{
    private static readonly int HeaderSize = 4 * IntPtr.Size;

    public static Il2CppReferenceArray<T> Of<T>(IReadOnlyList<T> items)
        where T : Il2CppObjectBase
    {
        var result = new Il2CppReferenceArray<T>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var slot = result.Pointer + HeaderSize + (i * IntPtr.Size);
            IL2CPP.il2cpp_gc_wbarrier_set_field(result.Pointer, slot, items[i]?.Pointer ?? IntPtr.Zero);
        }

        return result;
    }
}
