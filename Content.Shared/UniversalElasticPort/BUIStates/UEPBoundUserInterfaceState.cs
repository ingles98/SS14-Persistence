using System.Numerics;
using Content.Shared.Atmos;
using Robust.Shared.Serialization;

namespace Content.Shared.UniversalElasticPort.BUIStates;

[Serializable, NetSerializable, Virtual]
public class UEPAvailableConnection
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public Vector2 Position;
    public float Distance;
    public bool Occupied = false;
}

[Serializable, NetSerializable]
public sealed class UEPCurrentConnection : UEPAvailableConnection
{
    // Unsure if more information for the current connection will be needed.
}

[Serializable, NetSerializable]
public abstract class UEPBasePlugState
{
    public string Identifier { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;
    public bool IsNetworked { get; set; } = false;
    public bool IsRemoteEnabled { get; set; } = false;
}

[Serializable, NetSerializable]
public abstract class UEPCounterpartState;

[Serializable, NetSerializable]
public sealed class UEPPowerState : UEPCounterpartState
{
    // @TODO: Maybe power supply/demand
    public float CombinedLoad { get; set; }
    public float CombinedSupply { get; set; }
    public float CombinedMaxSupply { get; set; }
}

[Serializable, NetSerializable]
public sealed class UEPPipeState : UEPCounterpartState
{
    public GasMixture GasMix { get; set; } = GasMixture.SpaceGas;
}

[Serializable, NetSerializable]
public abstract class UEPPlugStateCounterparts<T>(T local, T remote) : UEPBasePlugState where T : UEPCounterpartState
{
    public T LocalState { get; set; } = local;
    public T RemoteState { get; set; } = remote;
}

[Serializable, NetSerializable]
public sealed class UEPPowerPlugState(UEPPowerState local, UEPPowerState remote) : UEPPlugStateCounterparts<UEPPowerState>(local, remote) { }
[Serializable, NetSerializable]
public sealed class UEPPipePlugState(UEPPipeState local, UEPPipeState remote) : UEPPlugStateCounterparts<UEPPipeState>(local, remote) { }

[Serializable, NetSerializable]
public sealed class UEPBoundUserInterfaceState : BoundUserInterfaceState
{
    public float MaxRange;
    public List<UEPAvailableConnection> AvailableConnections = [];
    public UEPCurrentConnection? CurrentConnection = null;
    public Dictionary<string, UEPBasePlugState> PlugStates = new();
}

[Serializable, NetSerializable]
public enum UEPConsoleUiKey : byte
{
    Key
}

#region Messages
[Serializable, NetSerializable]
public sealed class UEPConnectMessage(NetEntity target) : BoundUserInterfaceMessage
{
    public NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class UEPDisconnectMessage() : BoundUserInterfaceMessage {}


[Serializable, NetSerializable]
public sealed class UEPTogglePlugMessage(string identifier) : BoundUserInterfaceMessage
{
    public string Identifier = identifier;
}
#endregion
