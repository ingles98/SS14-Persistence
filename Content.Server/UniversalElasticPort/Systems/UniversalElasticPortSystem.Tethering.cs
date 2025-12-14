using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Piping.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Nodes;
using Content.Server.UniversalElasticPort.Components;
using Content.Shared.Coordinates;
using Content.Shared.NodeContainer.NodeGroups;
using Content.Shared.Physics;
using Robust.Shared.Utility;

namespace Content.Server.UniversalElasticPort.Systems;

public sealed partial class UniversalElasticPortSystem : EntitySystem
{
    /// <summary>
    /// How much will cable/hose joints be offset from the center of each port - currently random based on this value.
    /// </summary>
    private const float MaxJointOffset = 1f / 6f;

    private Dictionary<EntityUid, Dictionary<string, EntityUid>> _tethersByConnection = new();

    private void InitializeTethers()
    {
        SubscribeLocalEvent<UEPTetherComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<UEPTetherComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<UEPTetherComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Connection == default) return; // Initializing
        if (
            !ent.Comp.Connection.Valid ||
            TerminatingOrDeleted(ent.Comp.Connection) ||
            !_tethersByConnection.ContainsKey(ent.Comp.Connection) ||
            !_tethersByConnection[ent.Comp.Connection].ContainsKey(ent.Comp.NodeIdentifier) ||
            _tethersByConnection[ent.Comp.Connection][ent.Comp.NodeIdentifier] != ent.Owner
        )
        {
            QueueDel(ent);
            return;
        }
    }

    private void OnShutdown(Entity<UEPTetherComponent> ent, ref ComponentShutdown args)
    {
        if (
            _tethersByConnection.ContainsKey(ent.Comp.Connection) &&
            _tethersByConnection[ent.Comp.Connection].ContainsKey(ent.Comp.NodeIdentifier) &&
            _tethersByConnection[ent.Comp.Connection][ent.Comp.NodeIdentifier] == ent.Owner
        )
        {
            _tethersByConnection[ent.Comp.Connection].Remove(ent.Comp.NodeIdentifier);
        }
    }

    public void SetupTethering(Entity<UEPConnectionComponent> entity)
    {
        _tethersByConnection.Add(entity, new());
        UpdateTethering(entity);
    }

    public void BreakdownTethering(Entity<UEPConnectionComponent> entity)
    {
        if (!_tethersByConnection.TryGetValue(entity, out var nodeTethers)) return;
        foreach (var uid in nodeTethers.Values)
            QueueDel(uid);
        nodeTethers.Clear();
        _tethersByConnection.Remove(entity);
    }

    public void UpdateTethering(Entity<UEPConnectionComponent> entity)
    {
        if (!_tethersByConnection.TryGetValue(entity, out var nodeTethers))
            return;

        if (!TryComp<UEPComponent>(entity.Comp.AnchorA, out var uepA)) return;
        if (!TryComp<UEPComponent>(entity.Comp.AnchorB, out var uepB)) return;
        Entity<UEPComponent> anchorA = (entity.Comp.AnchorA, uepA);
        Entity<UEPComponent> anchorB = (entity.Comp.AnchorB, uepB);

        foreach (var key in uepA.EnabledPlugs.Keys.Concat(uepB.EnabledPlugs.Keys).Distinct())
        {
            if (uepA.EnabledPlugs.TryGetValue(key, out var value1) && uepB.EnabledPlugs.TryGetValue(key, out var value2) && value1 && value2)
            {
                EnsureCreateNodeTether(nodeTethers, entity, anchorA, anchorB, key);
            }
            else
                EnsureDeleteNodeTether(nodeTethers, entity, key);
        }
    }

    public void UpdateTethering(Entity<UEPComponent> entity)
    {
        if (IsConnected(entity) && TryComp<UEPConnectionComponent>(entity.Comp.Connection, out var conn))
            UpdateTethering((entity.Comp.Connection.GetValueOrDefault(), conn));
    }

    private void EnsureCreateNodeTether(Dictionary<string, EntityUid> nodeTethers, Entity<UEPConnectionComponent> entity, Entity<UEPComponent> anchorA, Entity<UEPComponent> anchorB, string key)
    {
        // TODO: On UepTetherComponentInit ()

        EntityUid tetherUid;
        if (!nodeTethers.TryGetValue(key, out var value))
        {
            tetherUid = SpawnAttachedTo("UEPTetherStub", anchorA.Owner.ToCoordinates());
            var tetherComp = EnsureComp<UEPTetherComponent>(tetherUid);
            tetherComp.Connection = entity;
            tetherComp.NodeIdentifier = key;
            nodeTethers.Add(key, tetherUid);
        }
        else
            tetherUid = value;

        if (!HasComp<JointVisualsComponent>(tetherUid))
        {
            var visuals = EnsureComp<JointVisualsComponent>(tetherUid);
            visuals.OffsetA = new Vector2(_random.NextFloat(-MaxJointOffset, MaxJointOffset), _random.NextFloat(-MaxJointOffset, MaxJointOffset));
            visuals.OffsetB = new Vector2(_random.NextFloat(-MaxJointOffset, MaxJointOffset), _random.NextFloat(-MaxJointOffset, MaxJointOffset));
            visuals.OffsetRotationMode = JointOffsetRotationMode.TowardsTarget;
            visuals.Target = anchorB;
            visuals.Sprite = new SpriteSpecifier.Rsi(new ResPath("Structures/Power/Cables/lv_cable.rsi"), "lvcable_3");

            // Find sprite.
            var node = GetPlugNode(anchorA, key);

            var physicalNode = node?.NodeGroup?.Nodes.FirstOrDefault(x => x is not PortPipeNode && x is not CableDeviceNode);
            if (physicalNode != null && physicalNode is PipeNode pipe && TryComp<AtmosPipeColorComponent>(pipe.Owner, out var pipeColor))
            {
                visuals.Sprite = new SpriteSpecifier.Rsi(new ResPath("Structures/Piping/Atmospherics/pipe.rsi"), "pipeStraight");
                visuals.Modulate = pipeColor.Color;
            }
            else if (physicalNode != null && physicalNode is CableNode cable)
            {
                if (cable.NodeGroup is BaseNodeGroup baseGroup)
                    visuals.Modulate = NodeGroupSystem.CalcNodeGroupColor(baseGroup);
                switch (cable.NodeGroupID)
                {
                    case NodeGroupID.HVPower:
                        {
                            visuals.Sprite = new SpriteSpecifier.Rsi(new ResPath("Structures/Power/Cables/hv_cable.rsi"), "hvcable_3");
                            break;
                        }
                    case NodeGroupID.MVPower:
                        {
                            visuals.Sprite = new SpriteSpecifier.Rsi(new ResPath("Structures/Power/Cables/mv_cable.rsi"), "mvcable_3");
                            visuals.SpriteOverlay = new SpriteSpecifier.Rsi(new ResPath("Structures/Power/Cables/mv_cable.rsi"), "mvstripes_3");
                            break;
                        }
                }
            }
            else
            {
                // Too early to load a tether it seems - next tick should be ok.
                QueueDel(tetherUid);
                nodeTethers.Remove(key);
                return;
            }
            Dirty(tetherUid, visuals);
        }
    }

    private void EnsureDeleteNodeTether(Dictionary<string, EntityUid> nodeTethers, Entity<UEPConnectionComponent> entity, string key)
    {
        if (nodeTethers.TryGetValue(key, out var tetherUid))
        {
            nodeTethers.Remove(key);
            if (!TerminatingOrDeleted(tetherUid))
                QueueDel(tetherUid);
        }
    }
}
