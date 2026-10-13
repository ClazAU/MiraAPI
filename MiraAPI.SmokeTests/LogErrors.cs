using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;

namespace MiraAPI.SmokeTests;

// Everything logged as an error or fatal since launch, read back from BepInEx's own disk log, so errors raised while
// MiraAPI loaded (before this plugin existed) count too.
public static class LogErrors
{
    public static List<string> SinceLaunch()
    {
        var path = DiskLogPath();
        if (path == null || !File.Exists(path)) return [$"no BepInEx disk log found (looked for {path})"];

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd()
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("[Error", StringComparison.Ordinal) || line.StartsWith("[Fatal", StringComparison.Ordinal))
            .ToList();
    }

    // Concurrent instances write LogOutput.log, LogOutput.1.log, ... by start order, so ask the listener which is ours.
    private static string? DiskLogPath()
    {
        var disk = BepInEx.Logging.Logger.Listeners.OfType<DiskLogListener>().FirstOrDefault();
        var writer = disk?.LogWriter as StreamWriter;
        if (writer?.BaseStream is FileStream file) return file.Name;

        var field = disk?.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .FirstOrDefault(f => f.FieldType == typeof(string) && f.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
        return field?.GetValue(disk) as string ?? Path.Combine(Paths.BepInExRootPath, "LogOutput.log");
    }
}
