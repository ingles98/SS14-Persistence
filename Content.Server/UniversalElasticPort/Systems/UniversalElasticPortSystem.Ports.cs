using Content.Server.UniversalElasticPort.Components;
using Content.Shared.NodeContainer;

namespace Content.Server.UniversalElasticPort.Systems;

public sealed partial class UniversalElasticPortSystem : EntitySystem
{
    private void InitializePlugs()
    {
        SubscribeLocalEvent<UEPComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<UEPComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<UEPComponent, AnchorStateChangedEvent>(OnAnchorStateChange);
    }

    private void OnStartup(Entity<UEPComponent> ent, ref ComponentStartup args)
    {
        CheckConnection(ent);
    }

    private void OnShutdown(Entity<UEPComponent> ent, ref ComponentShutdown args)
    {
        Disconnect(ent);
    }

    private void OnAnchorStateChange(Entity<UEPComponent> ent, ref AnchorStateChangedEvent args)
    {
        CheckConnection(ent);
    }

    private void ResetUep(Entity<UEPComponent> ent)
    {
        ent.Comp.Connection = default;
        ent.Comp.EnabledPlugs.Clear();

        if (TryComp<NodeContainerComponent>(ent, out var container) && container != null)
        {
            foreach (var node in container.Nodes)
            {
                _nodeGroup.QueueReflood(node.Value);
            }
        }
    }
}
