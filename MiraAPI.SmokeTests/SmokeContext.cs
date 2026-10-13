using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MiraAPI.SmokeTests;

public sealed class SmokeAbort : Exception;

// Freeplay is one client that owns every dummy, so everything here is local and nothing is an RPC.
public sealed class SmokeContext
{
    private readonly List<string> _failures = [];
    private readonly List<string> _notes = [];
    private int _shot;

    public string Test { get; private set; } = string.Empty;

    public bool Aborted { get; private set; }

    public static PlayerControl Local => PlayerControl.LocalPlayer;

    public static List<PlayerControl> Dummies => PlayerControl.AllPlayerControls.ToArray()
        .Where(p => p != null && p != PlayerControl.LocalPlayer).OrderBy(p => p.PlayerId).ToList();

    public void Begin(string name)
    {
        Test = name;
        Aborted = false;
        _failures.Clear();
        _notes.Clear();
        _shot = 0;
    }

    public SmokeResult End(float seconds)
    {
        var passed = _failures.Count == 0;
        var detail = passed ? string.Join("; ", _notes) : string.Join("; ", _failures.Concat(_notes.Select(n => "note: " + n)));
        return new SmokeResult(Test, passed, detail.Length == 0 ? "ok" : detail, seconds);
    }

    public void Fail(string what)
    {
        _failures.Add(what);
        SmokeRunner.Log($"  {Test}: check failed: {what}");
    }

    public void Note(string what)
    {
        _notes.Add(what);
        SmokeRunner.Log($"  {Test}: {what}");
    }

    public bool Check(bool condition, string what)
    {
        if (!condition) Fail(what);
        return condition;
    }

    public void Require(bool condition, string what)
    {
        if (condition) return;

        Fail(what);
        Aborted = true;
        throw new SmokeAbort();
    }

    public static IEnumerator Wait(float seconds)
    {
        var until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }

    public IEnumerator WaitUntil(Func<bool> condition, float timeout, string what)
    {
        var until = Time.time + timeout;
        while (!condition())
        {
            if (Time.time > until)
            {
                Require(false, $"timed out after {timeout}s waiting for {what}");
                yield break;
            }

            yield return null;
        }
    }

    public IEnumerator Screenshot(string label)
    {
        var file = $"{Test.Replace('.', '_')}-{++_shot:00}-{label}.png";
        ScreenCapture.CaptureScreenshot(Path.Combine(SmokeRunner.OutputDir, file));
        yield return null;
        yield return null;
    }

    public IEnumerator CoReset()
    {
        if (Minigame.Instance != null)
        {
            try
            {
                Minigame.Instance.Close();
            }
            catch (Exception ex)
            {
                SmokeRunner.Log($"closing the open minigame threw {ex.Message}");
            }

            yield return Wait(0.8f);
        }

        if (MeetingHud.Instance != null || ExileController.Instance != null) yield return FinishMeeting(PlayerVoteArea.SkippedVote);

        foreach (var player in PlayerControl.AllPlayerControls.ToArray())
        {
            if (player != null && player.Data != null && player.Data.IsDead) player.Revive();
        }

        if (Local.Data.Role.Role != RoleTypes.Crewmate) RoleManager.Instance.SetRole(Local, RoleTypes.Crewmate);
        Local.moveable = true;
        yield return Wait(0.3f);
    }

    // A dummy reports with no body; everyone alive votes for `target` (or skips) as soon as voting opens.
    public IEnumerator CallMeeting()
    {
        var reporter = Dummies.First(p => p.Data is { IsDead: false });
        reporter.RemainingEmergencies = 99;
        reporter.CmdReportDeadBody(null);
        yield return WaitUntil(() => MeetingHud.Instance != null, 6f, "the meeting to open");
    }

    public IEnumerator FinishMeeting(byte target)
    {
        var until = Time.time + 45f;
        while (MeetingHud.Instance != null || ExileController.Instance != null)
        {
            Require(Time.time < until, "the meeting never ended");

            var meeting = MeetingHud.Instance;
            if (meeting != null)
            {
                foreach (var player in PlayerControl.AllPlayerControls.ToArray().Where(p => p != null && p.Data is { IsDead: false }))
                {
                    try
                    {
                        if (!meeting.DidVote(player.PlayerId)) meeting.CmdCastVote(player.PlayerId, target);
                    }
                    catch (Exception)
                    {
                        // Votes before the voting phase opens are refused; the next pass casts them.
                    }
                }

                // Outside online games the results wait for the host to press Proceed; it's a no-op before then.
                meeting.HandleProceed();
            }

            yield return Wait(0.5f);
        }

        yield return Wait(0.8f);
    }

    public NormalPlayerTask AddTask(TaskTypes type)
    {
        foreach (var existing in Local.myTasks.ToArray().Where(t => t != null && t.TaskType == type).ToList())
        {
            Local.RemoveTask(existing);
        }

        var ship = ShipStatus.Instance;
        var prefab = ship.CommonTasks.Concat(ship.ShortTasks).Concat(ship.LongTasks).FirstOrDefault(t => t != null && t.TaskType == type);
        Require(prefab != null, $"Skeld has no {type} task");

        TaskAddButton.AddTaskToPlayer(prefab!);
        var task = Local.myTasks.ToArray().Last(t => t != null && t.TaskType == type).TryCast<NormalPlayerTask>();
        Require(task != null, $"{type} was not added as a NormalPlayerTask");
        return task!;
    }

    // Opens the task the way Console.Use does, without walking to a console.
    public IEnumerator Open(NormalPlayerTask task)
    {
        var console = ShipStatus.Instance.AllConsoles.ToArray().FirstOrDefault(c => c != null && c.TaskTypes.Contains(task.TaskType));
        var minigame = Object.Instantiate(task.GetMinigamePrefab(), Camera.main.transform, false);
        minigame.transform.localPosition = new Vector3(0f, 0f, -50f);
        minigame.Console = console;
        minigame.Begin(task);

        Require(Minigame.Instance != null, $"{task.TaskType} minigame did not open");
        yield return Wait(0.3f);
    }
}
