using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Il2CppInterop.Runtime;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using Reactor.Utilities;
using UnityEngine;

namespace MiraAPI.SmokeTests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SmokeTestAttribute(string name, float timeout = 30f, bool last = false) : Attribute
{
    public string Name { get; } = name;

    public float Timeout { get; } = timeout;

    // Runs after every other test, for a test that leaves the game.
    public bool Last { get; } = last;
}

public sealed record SmokeResult(string Name, bool Passed, string Detail, float Seconds);

// MIRA_SMOKE=all (or a comma list of names or prefixes) boots into Skeld Freeplay and runs the suite; MIRA_SMOKE_QUIT=1
// quits afterwards. Results go to BepInEx/mira-smoke. Run it after every game update, before shipping a MiraAPI build.
public static class SmokeRunner
{
    public const string LogTag = "[MiraSmoke]";

    private static readonly string? Filter = Environment.GetEnvironmentVariable("MIRA_SMOKE");
    private static readonly bool QuitWhenDone = Environment.GetEnvironmentVariable("MIRA_SMOKE_QUIT") == "1";

    private static bool _launched;
    private static bool _autoRunPending = !string.IsNullOrWhiteSpace(Filter);

    public static bool Running { get; private set; }

    public static string OutputDir => Path.Combine(Paths.BepInExRootPath, "mira-smoke");

    public static void Log(string message) => Message($"{LogTag} {message}");

    private static List<(SmokeTestAttribute Info, MethodInfo Method)> Discover(string? filter)
    {
        var wanted = string.IsNullOrWhiteSpace(filter) || filter.Trim() == "all"
            ? null
            : filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return typeof(SmokeRunner).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Select(method => (Info: method.GetCustomAttribute<SmokeTestAttribute>()!, Method: method))
            .Where(test => test.Info != null)
            .Where(test => wanted == null || wanted.Any(w => test.Info.Name == w || test.Info.Name.StartsWith(w + ".", StringComparison.Ordinal)))
            .OrderBy(test => test.Info.Last)
            .ThenBy(test => test.Method.MetadataToken)
            .ToList();
    }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    public static class LaunchFreeplayPatch
    {
        public static void Postfix()
        {
            if (!_autoRunPending || _launched) return;

            _launched = true;
            Coroutines.Start(CoLaunchFreeplay());
        }
    }

    private static IEnumerator CoLaunchFreeplay()
    {
        yield return new WaitForSeconds(6f);

        var popover = Resources.FindObjectsOfTypeAll(Il2CppType.Of<FreeplayPopover>())
            .Select(o => o.TryCast<FreeplayPopover>())
            .FirstOrDefault(p => p != null && p.gameObject.scene.IsValid());
        if (popover == null)
        {
            Log("FAIL harness: no FreeplayPopover in the main menu");
            yield break;
        }

        Log("launching Freeplay on Skeld");

        // The host button starts a coroutine, which needs the popover and every menu above it active.
        for (var node = popover.transform; node != null; node = node.parent)
        {
            if (!node.gameObject.activeSelf) node.gameObject.SetActive(true);
        }

        popover.Show();
        yield return new WaitForSeconds(0.5f);
        AccessTools.Method(typeof(FreeplayPopover), "PlayMap").Invoke(popover, [MapNames.Skeld]);
    }

    [RegisterEvent]
    public static void OnRoundStart(RoundStartEvent e)
    {
        if (!_autoRunPending || !e.TriggeredByIntro || !TutorialManager.InstanceExists) return;

        _autoRunPending = false;
        Coroutines.Start(CoRunSuite(Filter));
    }

    private static IEnumerator CoRunSuite(string? filter)
    {
        Running = true;
        var tests = Discover(filter);
        var results = new List<SmokeResult>();
        Directory.CreateDirectory(OutputDir);
        foreach (var png in Directory.GetFiles(OutputDir, "*.png")) File.Delete(png);

        yield return new WaitForSeconds(3f);

        var context = new SmokeContext();
        Log($"running {tests.Count} tests");

        foreach (var (info, method) in tests)
        {
            context.Begin(info.Name);
            var started = Time.realtimeSinceStartup;

            yield return Drive(context.CoReset(), 60f, context);
            if (!context.Aborted && method.Invoke(null, [context]) is IEnumerator body)
            {
                yield return Drive(body, info.Timeout, context);
            }

            var result = context.End(Time.realtimeSinceStartup - started);
            results.Add(result);
            Log($"{(result.Passed ? "PASS" : "FAIL")} {result.Name}: {result.Detail}");
            WriteResults(results, tests.Count);

            if (!TutorialManager.InstanceExists) break;
        }

        Log($"done: {results.Count(r => r.Passed)}/{results.Count} passed");
        WriteResults(results, tests.Count);
        Running = false;

        if (QuitWhenDone) Application.Quit();
    }

    // Runs a test coroutine by hand so a throw or a hang fails that test instead of silently killing the suite.
    private static IEnumerator Drive(IEnumerator root, float timeout, SmokeContext context)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(root);
        var deadline = Time.realtimeSinceStartup + timeout;

        while (stack.Count > 0)
        {
            if (context.Aborted) yield break;
            if (Time.realtimeSinceStartup > deadline)
            {
                context.Fail($"timed out after {timeout:0}s");
                yield break;
            }

            object? current;
            try
            {
                if (!stack.Peek().MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                current = stack.Peek().Current;
            }
            catch (SmokeAbort)
            {
                yield break;
            }
            catch (Exception ex)
            {
                var inner = ex is TargetInvocationException { InnerException: { } cause } ? cause : ex;
                context.Fail($"threw {inner.GetType().Name}: {inner.Message}");
                Error($"{LogTag} {context.Test} threw: {inner}");
                yield break;
            }

            if (current is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }

            yield return current;
        }
    }

    private static void WriteResults(List<SmokeResult> results, int total)
    {
        var lines = new List<string>
        {
            $"MiraAPI smoke tests, {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"{results.Count(r => r.Passed)}/{results.Count} passed ({total} selected)",
            string.Empty,
        };
        lines.AddRange(results.Select(r => $"{(r.Passed ? "PASS" : "FAIL")} {r.Name} ({r.Seconds:0.0}s): {r.Detail}"));
        File.WriteAllLines(Path.Combine(OutputDir, "results.txt"), lines);
    }
}
