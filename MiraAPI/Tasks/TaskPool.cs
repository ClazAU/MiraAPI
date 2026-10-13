namespace MiraAPI.Tasks;

/// <summary>
/// One of the ship's task pools: <see cref="ShipStatus.CommonTasks"/>, <see cref="ShipStatus.ShortTasks"/> or <see cref="ShipStatus.LongTasks"/>.
/// </summary>
public enum TaskPool : byte
{
    /// <summary>
    /// <see cref="ShipStatus.CommonTasks"/>.
    /// </summary>
    CommonTasks,

    /// <summary>
    /// <see cref="ShipStatus.ShortTasks"/>.
    /// </summary>
    ShortTasks,

    /// <summary>
    /// <see cref="ShipStatus.LongTasks"/>.
    /// </summary>
    LongTasks,
}
