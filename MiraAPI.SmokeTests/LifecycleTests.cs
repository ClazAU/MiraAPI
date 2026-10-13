using System.Collections;
using InnerNet;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Player;

namespace MiraAPI.SmokeTests;

public static class LifecycleTests
{
    private static bool? _leftFromExitGame;

    [RegisterEvent]
    public static void OnLocalClientLeft(LocalClientLeftEvent e)
    {
        if (SmokeRunner.Running) _leftFromExitGame ??= e.FromExitGame;
    }

    [SmokeTest("lifecycle.local_client_left", last: true)]
    public static IEnumerator LocalClientLeft(SmokeContext t)
    {
        _leftFromExitGame = null;
        AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame);
        yield return t.WaitUntil(() => _leftFromExitGame != null, 10f, "LocalClientLeftEvent");
        t.Check(_leftFromExitGame == true, "leaving Freeplay was not reported as ExitGame");
    }
}
