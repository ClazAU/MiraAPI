using InnerNet;

namespace MiraAPI.Networking;

/// <summary>
/// Guards for <c>[MethodRpc]</c> handlers that apply a host-authoritative broadcast.
/// </summary>
public static class RpcSenderExtensions
{
    /// <summary>
    /// Gets whether <paramref name="sender"/> is owned by the current host. A host-only broadcast handler starts with
    /// <c>if (!sender.IsFromHost()) return;</c>, since Reactor runs a handler on whoever calls it and any client can.
    /// Null-safe, unlike <see cref="Utilities.Extensions.IsHost"/>, and works on any <see cref="InnerNetObject"/>.
    /// </summary>
    /// <param name="sender">The object the RPC was sent on.</param>
    /// <returns><see langword="true"/> if the current host owns <paramref name="sender"/>.</returns>
    public static bool IsFromHost(this InnerNetObject? sender) =>
        sender != null && AmongUsClient.Instance != null && sender.OwnerId == AmongUsClient.Instance.HostId;
}
