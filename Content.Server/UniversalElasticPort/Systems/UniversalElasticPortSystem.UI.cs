using System.Linq;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.NodeGroups;
using Content.Server.Power.Nodes;
using Content.Server.UniversalElasticPort.Components;
using Content.Shared.Coordinates;
using Content.Shared.NodeContainer;
using Content.Shared.UniversalElasticPort.BUIStates;
using Robust.Server.GameObjects;

namespace Content.Server.UniversalElasticPort.Systems;

public sealed partial class UniversalElasticPortSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    private void InitializeUI()
    {
        Subs.BuiEvents<UEPComponent>(UEPConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpen);
            subs.Event<UEPConnectMessage>(OnConnectRequest);
            subs.Event<UEPDisconnectMessage>(OnDisconnectRequest);
            subs.Event<UEPTogglePlugMessage>(OnTogglePortRequest);
        });
    }

    private void OnUiOpen(EntityUid uid, UEPComponent component, BoundUIOpenedEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void UpdateUserInterface(EntityUid uid, UEPComponent component)
    {
        if (_uiSystem.HasUi(uid, UEPConsoleUiKey.Key))
        {
            if (!TryComp<NodeContainerComponent>(uid, out var container))
                return;
            var state = GetInterfaceState(new Entity<UEPComponent, NodeContainerComponent>(uid, component, container));
            _uiSystem.SetUiState(uid, UEPConsoleUiKey.Key, state);
        }
    }

    public UEPBoundUserInterfaceState GetInterfaceState(Entity<UEPComponent, NodeContainerComponent> entity)
    {
        // container.Nodes.ToDictionary(x => x.Key, x => x.Value.NodeGroup?.Nodes.Count > 1);
        var availableConnections = (entity.Comp1.Connection.GetValueOrDefault() != default ? [] : GetAvailableConnections(entity))
            .Select(x =>
            {
                _physx.TryGetDistance(entity, x, out float distance);
                return new UEPAvailableConnection()
                {
                    Entity = GetNetEntity(x),
                    Name = Comp<MetaDataComponent>(x.Owner).EntityName,
                    Distance = distance,
                    Occupied = x.Comp.Connection.GetValueOrDefault() != default,
                    Position = x.Owner.ToCoordinates().Position,
                };
            });

        UEPCurrentConnection? currentConnectionState = null;
        var currentConnectionEid = entity.Comp1.Connection.GetValueOrDefault();
        TryComp<UEPConnectionComponent>(currentConnectionEid, out var connection);

        var counterpart = connection != null ? GetConnectionCounterpart(entity, connection) : default;
        TryComp<UEPComponent>(counterpart, out var counterpartUep);

        if (connection != null)
        {

            _physx.TryGetDistance(entity, counterpart, out var distance);
            currentConnectionState = new()
            {
                Entity = GetNetEntity(counterpart),
                Name = Comp<MetaDataComponent>(counterpart).EntityName,
                Distance = distance,
                Occupied = true,
                Position = counterpart.ToCoordinates().Position,
            };
        }

        // Plug states
        var plugStates = new Dictionary<string, UEPBasePlugState>();
        foreach (var plugEntry in GetPlugNodes(entity.Owner))
        {
            var counterpartNode = counterpart != default ? GetPlugNode(counterpart!, plugEntry.Key) : null;

            UEPBasePlugState portData;
            if (plugEntry.Value is CableDeviceNode deviceNode)
            {
                static UEPPowerState FromNet(PowerNet net) =>
                    new()
                    {
                        CombinedLoad = net.NetworkNode.LastCombinedLoad,
                        CombinedSupply = net.NetworkNode.LastCombinedSupply,
                        CombinedMaxSupply = net.NetworkNode.LastCombinedMaxSupply
                    };

                UEPPowerState localState = new();
                if (deviceNode.NodeGroup is PowerNet net)
                    localState = FromNet(net);

                UEPPowerState remoteState = new();
                if (counterpartNode != null && counterpartNode is CableDeviceNode && counterpartNode.NodeGroup is PowerNet remoteNet)
                    remoteState = FromNet(remoteNet);

                portData = new UEPPowerPlugState(localState, remoteState);
            }
            else if (plugEntry.Value is PortPipeNode pipeNode)
            {
                static UEPPipeState FromNet(PipeNet net) =>
                    new()
                    {
                        GasMix = net.Air,
                    };

                UEPPipeState localState = new();
                if (pipeNode.NodeGroup is PipeNet net)
                    localState = FromNet(net);

                UEPPipeState remoteState = new();
                if (counterpartNode != null && counterpartNode is PortPipeNode && counterpartNode.NodeGroup is PipeNet remoteNet)
                    remoteState = FromNet(remoteNet);

                portData = new UEPPipePlugState(localState, remoteState);
            }
            else
                continue;

            portData.Identifier = plugEntry.Key;
            portData.Enabled = IsPlugEnabled(entity.Comp1, plugEntry.Key);
            portData.IsNetworked = HasNodeNetwork(plugEntry.Value);
            portData.IsRemoteEnabled = counterpartUep != null && IsPlugEnabled(counterpartUep, plugEntry.Key);
            plugStates.Add(plugEntry.Key, portData);
        }

        return new()
        {
            MaxRange = entity.Comp1.MaxRange,
            AvailableConnections = availableConnections.ToList(),
            CurrentConnection = currentConnectionState,
            PlugStates = plugStates,
        };
    }

    private void OnConnectRequest(EntityUid uid, UEPComponent component, UEPConnectMessage args)
    {
        if (component.Connection.HasValue) return;
        Connect(uid, GetEntity(args.Target));
    }

    private void OnDisconnectRequest(EntityUid uid, UEPComponent component, UEPDisconnectMessage args)
    {
        if (!component.Connection.HasValue) return;
        Disconnect((uid, component));
    }

    private void OnTogglePortRequest(EntityUid uid, UEPComponent component, UEPTogglePlugMessage args)
    {
        TogglePlugState((uid, component), args.Identifier);
    }
}
