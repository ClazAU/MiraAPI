using System.Collections;
using System.Linq;
using AmongUs.GameOptions;
using MiraAPI.Events;
using MiraAPI.Events.Mira;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using MiraAPI.Utilities;

namespace MiraAPI.SmokeTests;

// Regression tests for behaviour mods used to work around: each one failed before the fix it names.
public static class TrapTests
{
    private const string EjectionSuffix = " [smoke]";

    private static bool? _exileGoneAtRoundStart;
    private static bool? _canMoveAtRoundStart;
    private static bool _vetoButtons;

    [RegisterEvent]
    public static void OnEjection(EjectionEvent e)
    {
        if (SmokeRunner.Running) e.ExileController.completeString += EjectionSuffix;
    }

    [RegisterEvent]
    public static void OnRoundStart(RoundStartEvent e)
    {
        if (!SmokeRunner.Running || e.TriggeredByIntro) return;

        _exileGoneAtRoundStart = ExileController.Instance == null;
        _canMoveAtRoundStart = PlayerControl.LocalPlayer.CanMove;
    }

    [RegisterEvent]
    public static void OnButtonCanUse(MiraButtonCanUseEvent e)
    {
        if (_vetoButtons && e.Button is SmokeButton) e.Cancel();
    }

    [SmokeTest("modifiers.see_self_in_hooks")]
    public static IEnumerator ModifierHooksSeeChange(SmokeContext t)
    {
        SmokeModifier.SawSelfOnActivate = null;
        SmokeModifier.SawSelfOnDeactivate = null;

        var modifier = SmokeContext.Local.AddModifier<SmokeModifier>();
        yield return SmokeContext.Wait(0.2f);
        t.Check(SmokeModifier.SawSelfOnActivate == true, $"HasModifier inside OnActivate was {SmokeModifier.SawSelfOnActivate}");

        SmokeContext.Local.RemoveModifier(modifier!);
        yield return SmokeContext.Wait(0.2f);
        t.Check(SmokeModifier.SawSelfOnDeactivate == false, $"HasModifier inside OnDeactivate was {SmokeModifier.SawSelfOnDeactivate}");
    }

    [SmokeTest("modifiers.remove_pending")]
    public static IEnumerator RemovePending(SmokeContext t)
    {
        var errorsBefore = LogErrors.SinceLaunch().Count;
        SmokeModifier.Activations = 0;

        var modifier = SmokeContext.Local.AddModifier<SmokeModifier>();
        SmokeContext.Local.RemoveModifier(modifier!);
        yield return SmokeContext.Wait(0.2f);

        t.Check(SmokeModifier.Activations == 0, "a modifier removed before its first tick still activated");
        t.Check(!SmokeContext.Local.HasModifier<SmokeModifier>(), "a modifier removed before its first tick is active");

        SmokeContext.Local.RemoveModifier<SmokeModifier>();
        yield return SmokeContext.Wait(0.2f);
        t.Check(LogErrors.SinceLaunch().Count == errorsBefore, "removing an absent modifier logged an error");
    }

    [SmokeTest("buttons.reshow_after_ejection", 60f)]
    public static IEnumerator ButtonBackAfterEjection(SmokeContext t)
    {
        RoleManager.Instance.SetRole(SmokeContext.Local, (RoleTypes)RoleId.Get<SmokeRole>());
        yield return SmokeContext.Wait(0.5f);

        var button = CustomButtonSingleton<SmokeButton>.Instance;
        t.Require(button.Button != null && button.Button.gameObject.activeInHierarchy, "the button never showed for its role");

        _exileGoneAtRoundStart = null;
        _canMoveAtRoundStart = null;
        yield return t.CallMeeting();
        t.Check(!button.Button!.gameObject.activeInHierarchy, "the button stayed up during the meeting");
        yield return t.FinishMeeting(PlayerVoteArea.SkippedVote);
        yield return SmokeContext.Wait(0.5f);

        t.Check(button.Button.gameObject.activeInHierarchy, "the button stayed hidden after the ejection");
        t.Check(_exileGoneAtRoundStart == true, $"ExileController.Instance was still set at the post-meeting RoundStartEvent ({_exileGoneAtRoundStart})");
        t.Check(_canMoveAtRoundStart == true, $"CanMove at the post-meeting RoundStartEvent was {_canMoveAtRoundStart}");
    }

    [SmokeTest("buttons.can_use_veto")]
    public static IEnumerator ButtonVeto(SmokeContext t)
    {
        RoleManager.Instance.SetRole(SmokeContext.Local, (RoleTypes)RoleId.Get<SmokeRole>());
        yield return SmokeContext.Wait(0.5f);

        var button = CustomButtonSingleton<SmokeButton>.Instance;
        t.Require(button.Button != null, "no Smoke button");
        var click = button.Button!.GetComponent<PassiveButton>();

        _vetoButtons = true;
        SmokeButton.Clicks = 0;
        yield return SmokeContext.Wait(0.2f);
        click.OnClick.Invoke();
        t.Check(SmokeButton.Clicks == 0, "a vetoed button still clicked");
        t.Check(!button.Button.canInteract, "a vetoed button still lit up");

        _vetoButtons = false;
        yield return SmokeContext.Wait(0.2f);
        click.OnClick.Invoke();
        t.Check(SmokeButton.Clicks == 1, $"the button clicked {SmokeButton.Clicks} times once the veto lifted");
    }

    [SmokeTest("meetings.ejection_text", 60f)]
    public static IEnumerator EjectionText(SmokeContext t)
    {
        var target = SmokeContext.Dummies.First(p => p.Data is { IsDead: false });
        RoleManager.Instance.SetRole(target, (RoleTypes)RoleId.Get<SmokeRole>());
        GameOptionsManager.Instance.CurrentGameOptions.SetBool(BoolOptionNames.ConfirmImpostor, true);

        yield return t.CallMeeting();
        var seen = string.Empty;
        var finish = t.FinishMeeting(target.PlayerId);
        while (finish.MoveNext())
        {
            if (ExileController.Instance != null) seen = ExileController.Instance.completeString;
            yield return finish.Current;
        }

        t.Check(seen.Contains(SmokeRole.EjectionMessage) && seen.EndsWith(EjectionSuffix, System.StringComparison.Ordinal), $"ejection text was \"{seen}\"");
    }

    // The local player votes: Freeplay dummies cast their own votes and may already have none left.
    [SmokeTest("voting.refund_for_dead_suspect", 60f)]
    public static IEnumerator RefundForDeadSuspect(SmokeContext t)
    {
        var voter = SmokeContext.Local;
        var suspect = SmokeContext.Dummies.FirstOrDefault(p => p.Data is { IsDead: false });
        t.Require(suspect != null, "needs a living dummy");

        yield return t.CallMeeting();
        yield return t.WaitUntil(() => MeetingHud.Instance.state is MeetingHud.MeetingStates.NotVoted or MeetingHud.MeetingStates.Voted, 15f, "voting to open");

        MeetingHud.Instance.CmdCastVote(voter.PlayerId, suspect!.PlayerId);
        yield return SmokeContext.Wait(0.3f);
        t.Require(voter.GetVoteData().VotedFor(suspect.PlayerId), "the vote was not recorded");

        suspect.Die(DeathReason.Kill, false);
        yield return SmokeContext.Wait(0.3f);
        var votes = voter.GetVoteData();
        t.Check(!votes.VotedFor(suspect.PlayerId) && votes.VotesRemaining == 1, $"after the suspect died the voter still votes for them ({votes.VotedFor(suspect.PlayerId)}) with {votes.VotesRemaining} votes left");

        MeetingHud.Instance.CmdCastVote(voter.PlayerId, suspect.PlayerId);
        yield return SmokeContext.Wait(0.3f);
        t.Check(!votes.VotedFor(suspect.PlayerId) && votes.VotesRemaining == 1, "a vote for a dead player was accepted");

        yield return t.FinishMeeting(PlayerVoteArea.SkippedVote);
    }

    [SmokeTest("options.float_override")]
    public static IEnumerator FloatOverride(SmokeContext t)
    {
        var options = GameOptionsManager.Instance.CurrentGameOptions;
        var vanilla = options.GetFloat(FloatOptionNames.KillCooldown);
        float? Source(FloatOptionNames option) => option == FloatOptionNames.KillCooldown ? 7.5f : null;

        VanillaOptionOverrides.AddFloatSource(Source);
        var read = options.GetFloat(FloatOptionNames.KillCooldown);
        var logic = GameManager.Instance.LogicOptions.GetKillCooldown();
        VanillaOptionOverrides.RemoveFloatSource(Source);

        t.Check(read == 7.5f, $"GetFloat(KillCooldown) read {read} under the override");
        t.Check(logic == 7.5f, $"LogicOptions.GetKillCooldown read {logic} under the override");
        t.Check(options.GetFloat(FloatOptionNames.KillCooldown) == vanilla, "the override outlived its removal");
        yield break;
    }

    [SmokeTest("roles.ghost_settings_shown")]
    public static IEnumerator GhostSettingsShown(SmokeContext t)
    {
        var ghost = CustomRoleManager.CustomMiraRoles.OfType<SmokeGhostRole>().FirstOrDefault();
        t.Require(ghost != null, "the smoke ghost role was not registered");
        t.Check(!ghost!.Configuration.HideSettings, "a ghost role hides its count and chance by default");
        yield break;
    }
}
