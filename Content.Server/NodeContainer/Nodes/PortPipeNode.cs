using Content.Shared.NodeContainer;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Server.UniversalElasticPort.Components;
using Content.Server.UniversalElasticPort.Systems;

namespace Content.Server.NodeContainer.Nodes
{
    [DataDefinition]
    public sealed partial class PortPipeNode : PipeNode
    {
        public override IEnumerable<Node> GetReachableNodes(TransformComponent xform,
            EntityQuery<NodeContainerComponent> nodeQuery,
            EntityQuery<TransformComponent> xformQuery,
            MapGridComponent? grid,
            IEntityManager entMan)
        {
            if (!xform.Anchored || grid == null)
                yield break;

            var gridIndex = grid.TileIndicesFor(xform.Coordinates);

            if (entMan.TryGetComponent<UEPComponent>(Owner, out var uep) && entMan.TrySystem<UniversalElasticPortSystem>(out var uepSys))
            {
                var remoteNode = uepSys.GetRemoteConnectionFor(Owner, uep, this);
                if (remoteNode != null)
                    yield return remoteNode;
            }

            foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, grid, gridIndex))
            {
                if (node is PortablePipeNode)
                    yield return node;
            }

            foreach (var node in base.GetReachableNodes(xform, nodeQuery, xformQuery, grid, entMan))
            {
                yield return node;
            }
        }
    }
}
