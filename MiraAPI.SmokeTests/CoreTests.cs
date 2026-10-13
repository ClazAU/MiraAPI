using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Minigames;
using MiraAPI.Events.Vanilla.Player;
using MiraAPI.Networking;
using MiraAPI.Tasks;
using MiraAPI.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MiraAPI.SmokeTests;

public static class CoreTests
{
    private static readonly List<int> BeforeOpens = [];
    private static readonly List<int> AfterOpens = [];
    private static readonly List<uint> CompletedTaskIds = [];
    private static bool _introRoundStarted;

    [RegisterEvent]
    public static void OnRoundStart(RoundStartEvent e)
    {
        if (e.TriggeredByIntro) _introRoundStarted = true;
    }

    [RegisterEvent]
    public static void OnBeforeOpen(BeforeMinigameOpenEvent e) => BeforeOpens.Add(e.GameObject.GetInstanceID());

    [RegisterEvent]
    public static void OnOpen(MinigameOpenEvent e) => AfterOpens.Add(e.GameObject.GetInstanceID());

    [RegisterEvent]
    public static void OnCompleteTask(CompleteTaskEvent e) => CompletedTaskIds.Add(e.Task.Id);

    [SmokeTest("load.clean_log")]
    public static IEnumerator CleanLog(SmokeContext t)
    {
        var errors = LogErrors.SinceLaunch();
        t.Check(errors.Count == 0, $"{errors.Count} errors logged since launch: {string.Join(" | ", errors.Take(5))}");
        yield break;
    }

    [SmokeTest("events.intro_round_start")]
    public static IEnumerator IntroRoundStart(SmokeContext t)
    {
        t.Check(_introRoundStarted, "RoundStartEvent never fired for the intro");
        yield break;
    }

    [SmokeTest("events.minigame_open")]
    public static IEnumerator MinigameOpen(SmokeContext t)
    {
        BeforeOpens.Clear();
        AfterOpens.Clear();
        yield return t.Open(t.AddTask(TaskTypes.FixWiring));
        CheckOpenedOnce(t, Minigame.Instance);

        yield return t.CoReset();
        BeforeOpens.Clear();
        AfterOpens.Clear();
        yield return t.Open(t.AddTask(TaskTypes.FuelEngines));
        var stage = Minigame.Instance.GetComponentInChildren<RefuelStage>();
        t.Require(stage != null, "fuel opened without its refuel stage");
        CheckOpenedOnce(t, Minigame.Instance);
        CheckOpenedOnce(t, stage!);
    }

    [SmokeTest("tasks.give_and_complete")]
    public static IEnumerator GiveAndComplete(SmokeContext t)
    {
        var dummy = SmokeContext.Dummies.First();
        var held = dummy.myTasks.Count;
        var records = dummy.Data.Tasks.Count;

        var given = dummy.RpcGiveRandomTask();
        yield return SmokeContext.Wait(0.3f);

        t.Require(given != null, "RpcGiveRandomTask gave nothing");
        t.Check(dummy.myTasks.Count == held + 1 && dummy.Data.Tasks.Count == records + 1, $"{dummy.myTasks.Count - held} tasks and {dummy.Data.Tasks.Count - records} records added");

        var task = dummy.myTasks.ToArray().Last();
        t.Check(task.TaskType == given!.TaskType && task.Owner == dummy, $"added {task.TaskType}, expected {given.TaskType}");

        CompletedTaskIds.Clear();
        dummy.CompleteTask(task.Id);
        t.Check(CompletedTaskIds.Contains(task.Id), "CompleteTaskEvent did not fire for the given task");
    }

    [SmokeTest("utilities.il2cpp_arrays")]
    public static IEnumerator Il2CppArraysWrite(SmokeContext t)
    {
        var a = new GameObject("smoke-a");
        var b = new GameObject("smoke-b");
        var array = Il2CppArrays.Of(a, null!);
        Il2CppArrays.Set(array, 1, b);
        var grown = Il2CppArrays.Append(array, a);

        t.Check(array.Length == 2 && array[0] == a && array[1] == b, "Of/Set stored the wrong elements");
        t.Check(grown.Length == 3 && grown[2] == a && grown[1] == b, "Append lost or misplaced an element");
        t.Check(Il2CppArrays.Append<GameObject>(null, b).Length == 1, "Append on null did not start an empty array");

        Object.Destroy(a);
        Object.Destroy(b);
        yield break;
    }

    [SmokeTest("networking.is_from_host")]
    public static IEnumerator IsFromHost(SmokeContext t)
    {
        t.Check(SmokeContext.Local.IsFromHost(), "the Freeplay host's own player is not from the host");
        t.Check(!((PlayerControl?)null).IsFromHost(), "a null sender counted as the host");
        yield break;
    }

    private static void CheckOpenedOnce(SmokeContext t, Component panel)
    {
        var id = panel.gameObject.GetInstanceID();
        var before = BeforeOpens.Count(i => i == id);
        var after = AfterOpens.Count(i => i == id);
        t.Check(before == 1 && after == 1, $"{panel.GetIl2CppType().Name}: Before fired {before} times, After {after}");
    }
}
