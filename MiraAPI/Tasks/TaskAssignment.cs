using System.Collections.Generic;
using System.Linq;
using MiraAPI.Networking;
using MiraAPI.Utilities;
using Reactor.Networking.Attributes;
using UnityEngine;

namespace MiraAPI.Tasks;

/// <summary>
/// Gives a player an extra task mid-round on every client, without vanilla's <c>RpcSetTasks</c>, which wipes progress.
/// The host decides the task and its id; every client then adds it to the player's task list and task bar.
/// </summary>
public static class TaskAssignment
{
    // Vanilla gives sabotage tasks this id.
    private const uint SabotageTaskId = 255;

    /// <summary>
    /// Gets the task at <paramref name="index"/> in one of the current ship's pools.
    /// </summary>
    /// <param name="pool">The pool.</param>
    /// <param name="index">The position in that pool's array.</param>
    /// <returns>The task prefab, or <see langword="null"/> if there is no ship or no such task.</returns>
    public static NormalPlayerTask? GetPoolTask(TaskPool pool, int index)
    {
        var tasks = PoolTasks(pool);
        return tasks != null && index >= 0 && index < tasks.Length ? tasks[index] : null;
    }

    /// <summary>
    /// Host only: gives <paramref name="player"/> the task at <paramref name="index"/> in <paramref name="pool"/> on every client.
    /// </summary>
    /// <param name="player">The player to give the task to.</param>
    /// <param name="pool">The pool the task comes from.</param>
    /// <param name="index">The task's position in that pool's array.</param>
    /// <returns><see langword="true"/> if the task was sent; <see langword="false"/> off the host, without a ship or task, or once the player is out of task ids.</returns>
    public static bool RpcGiveTask(this PlayerControl player, TaskPool pool, int index)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return false;
        if (player == null || player.Data == null || GetPoolTask(pool, index) == null || index > byte.MaxValue) return false;

        var id = NextTaskId(player);
        if (id >= SabotageTaskId) return false;

        PlayerControl.LocalPlayer.RpcSyncGivenTask(player, (byte)pool, (byte)index, id);
        return true;
    }

    /// <summary>
    /// Host only: gives <paramref name="player"/> a random task from <paramref name="pools"/> on every client,
    /// skipping task types the player already has and tasks with no console on this ship.
    /// </summary>
    /// <param name="player">The player to give the task to.</param>
    /// <param name="pools">The pools to draw from; all of them when empty.</param>
    /// <returns>The task prefab given, or <see langword="null"/> if nothing was.</returns>
    public static NormalPlayerTask? RpcGiveRandomTask(this PlayerControl player, params TaskPool[] pools)
    {
        if (player == null || ShipStatus.Instance == null) return null;
        if (pools.Length == 0) pools = [TaskPool.CommonTasks, TaskPool.ShortTasks, TaskPool.LongTasks];

        var owned = player.myTasks.ToArray().Where(task => task != null).Select(task => task.TaskType).ToHashSet();
        var candidates = new List<(TaskPool Pool, int Index, NormalPlayerTask Task)>();
        foreach (var pool in pools.Distinct())
        {
            var tasks = PoolTasks(pool);
            for (var i = 0; tasks != null && i < tasks.Length; i++)
            {
                var task = tasks[i];
                if (task != null && !owned.Contains(task.TaskType) && HasConsole(task.TaskType)) candidates.Add((pool, i, task));
            }
        }

        if (candidates.Count == 0) return null;

        var pick = candidates[Random.Range(0, candidates.Count)];
        return player.RpcGiveTask(pick.Pool, pick.Index) ? pick.Task : null;
    }

    /// <summary>
    /// Applies a task the host gave. Call <see cref="RpcGiveTask"/> or <see cref="RpcGiveRandomTask"/> instead.
    /// </summary>
    /// <param name="host">The host's <see cref="PlayerControl"/>.</param>
    /// <param name="player">The player receiving the task.</param>
    /// <param name="pool">The <see cref="TaskPool"/> the task comes from.</param>
    /// <param name="index">The task's position in that pool's array.</param>
    /// <param name="id">The task id the host assigned.</param>
    [MethodRpc((uint)MiraRpc.GiveTask)]
    public static void RpcSyncGivenTask(this PlayerControl host, PlayerControl player, byte pool, byte index, uint id)
    {
        if (host == null || !host.IsHost() || player == null || player.Data == null || id >= SabotageTaskId) return;

        var prefab = GetPoolTask((TaskPool)pool, index);
        if (prefab == null) return;

        // The host's player data can arrive before this RPC and already hold the id, so each half is checked on its own.
        if (player.Data.FindTaskById(id) == null)
        {
            player.Data.Tasks.Add(new NetworkedPlayerInfo.TaskInfo((byte)prefab.Index, id));
        }

        if (!player.myTasks.ToArray().Any(task => task != null && task.Id == id))
        {
            var task = Object.Instantiate(prefab, player.transform);
            task.Id = id;
            task.Index = prefab.Index;
            task.Owner = player;
            task.Initialize();
            player.myTasks.Add(task);
        }

        GameData.Instance.RecomputeTaskCounts();
        if (AmongUsClient.Instance.AmHost) player.Data.MarkDirty();
    }

    private static NormalPlayerTask[]? PoolTasks(TaskPool pool)
    {
        var ship = ShipStatus.Instance;
        if (ship == null) return null;

        return pool switch
        {
            TaskPool.CommonTasks => ship.CommonTasks,
            TaskPool.ShortTasks => ship.ShortTasks,
            TaskPool.LongTasks => ship.LongTasks,
            _ => null,
        };
    }

    private static bool HasConsole(TaskTypes type)
        => ShipStatus.Instance.AllConsoles.Any(console => console != null && console.TaskTypes.Contains(type));

    private static uint NextTaskId(PlayerControl player)
    {
        var used = player.Data.Tasks.ToArray().Select(task => task.Id)
            .Concat(player.myTasks.ToArray().Where(task => task != null).Select(task => task.Id))
            .Where(id => id < SabotageTaskId);
        return used.Any() ? used.Max() + 1 : 0;
    }
}
