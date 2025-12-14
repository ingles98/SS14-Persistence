using Content.Server.UniversalElasticPort.Components;
using Content.Shared.Coordinates;
using Robust.Shared.Timing;

namespace Content.Server.UniversalElasticPort.Systems;

public sealed partial class UniversalElasticPortSystem : EntitySystem
{
    private static readonly TimeSpan UpdateDelay = TimeSpan.FromSeconds(1);

    private void InitializeConnections()
    {
        SubscribeLocalEvent<UEPConnectionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<UEPConnectionComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<UEPConnectionComponent> ent, ref ComponentStartup args)
    {
        CheckConnection(ent);
        BeginTrackingConnection(ent);
    }
    private void OnShutdown(Entity<UEPConnectionComponent> ent, ref ComponentShutdown args)
    {
        // Ensure clean connection severence.
        if (ent.Comp.AnchorA.Valid)
            CheckConnection(ent.Comp.AnchorA);
        if (ent.Comp.AnchorB.Valid)
            CheckConnection(ent.Comp.AnchorB);

        StopTrackingConnection(ent);
    }

    private void BeginTrackingConnection(Entity<UEPConnectionComponent> ent)
    {
        SetupTethering(ent);
        Timer.Spawn((int)(ent.Comp.NextUpdate - _gameTiming.CurTime).TotalMilliseconds, () => TimerFired(ent));
    }

    private void TimerFired(Entity<UEPConnectionComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (!CheckConnection(ent))
            return;

        UpdateTethering(ent);

        ent.Comp.NextUpdate += UpdateDelay;

        var ms = (int)(ent.Comp.NextUpdate - _gameTiming.CurTime).TotalMilliseconds;
        Timer.Spawn(ms, () => TimerFired(ent));
    }

    private void StopTrackingConnection(Entity<UEPConnectionComponent> ent)
    {
        BreakdownTethering(ent);
    }

    public bool IsConnected(Entity<UEPComponent> ent) => ent.Comp.Connection != default && !TerminatingOrDeleted(ent.Comp.Connection);

    public EntityUid GetConnectionCounterpart(EntityUid source, UEPConnectionComponent conn)
    {
        return source == conn.AnchorA ? conn.AnchorB : conn.AnchorA;
    }

    public bool CanKeepConnection(Entity<UEPComponent> ent)
    {
        if (!IsConnected(ent) || !TryComp<UEPConnectionComponent>(ent.Comp.Connection, out var conn)) return false;
        return CanKeepConnection((ent.Comp.Connection.GetValueOrDefault(), conn));
    }

    public bool CanKeepConnection(Entity<UEPConnectionComponent> connection)
    {
        if (!TryComp<UEPComponent>(connection.Comp.AnchorA, out var uepA)) return false;
        if (!TryComp<UEPComponent>(connection.Comp.AnchorB, out var uepB)) return false;
        return CanKeepConnection((connection.Comp.AnchorA, uepA), (connection.Comp.AnchorB, uepB));
    }

    public bool CanKeepConnection(Entity<UEPComponent> anchorA, Entity<UEPComponent> anchorB)
    {
        var xformA = Comp<TransformComponent>(anchorA);
        var xformB = Comp<TransformComponent>(anchorB);

        if (!xformA.Anchored || !xformB.Anchored) return false;

        var maxDistance = MathF.Min(anchorA.Comp.MaxRange, anchorB.Comp.MaxRange);

        return _transform.InRange(anchorA.Owner.ToCoordinates(), anchorB.Owner.ToCoordinates(), maxDistance);
    }

    public bool CanConnect(Entity<UEPComponent> anchorA, Entity<UEPComponent> anchorB)
    {
        if (anchorA.Comp.Connection.HasValue || anchorB.Comp.Connection.HasValue) return false;
        return CanKeepConnection(anchorA, anchorB);
    }

    public void Connect(EntityUid anchorA, EntityUid anchorB)
    {
        if (!TryComp<UEPComponent>(anchorA, out var uepA)) return;
        if (!TryComp<UEPComponent>(anchorB, out var uepB)) return;
        Connect((anchorA, uepA), (anchorB, uepB));
    }

    public void Connect(Entity<UEPComponent> anchorA, Entity<UEPComponent> anchorB)
    {
        if (anchorA.Comp.Connection.HasValue && TryComp<UEPConnectionComponent>(anchorA.Comp.Connection, out var connectionA))
            Disconnect((anchorA.Comp.Connection.Value, connectionA));
        if (anchorB.Comp.Connection.HasValue && TryComp<UEPConnectionComponent>(anchorB.Comp.Connection, out var connectionB))
            Disconnect((anchorB.Comp.Connection.Value, connectionB));

        // Ensure anchors are reset.
        ResetUep(anchorA);
        ResetUep(anchorB);

        var newConnectionEid = SpawnAttachedTo(null, anchorA.Owner.ToCoordinates());
        var newConnection = EnsureComp<UEPConnectionComponent>(newConnectionEid);

        anchorA.Comp.Connection = anchorB.Comp.Connection = newConnectionEid;
        newConnection.AnchorA = anchorA;
        newConnection.AnchorB = anchorB;

        OnConnectionEstablished((newConnectionEid, newConnection), anchorA, anchorB);
    }

    private void OnConnectionEstablished(Entity<UEPConnectionComponent> newConnection, Entity<UEPComponent> anchorA, Entity<UEPComponent> anchorB)
    {
        var evA = new UEPConnected(anchorA, anchorA.Comp, newConnection);
        RaiseLocalEvent(anchorA, ref evA, true);
        OnConnectionChanged(anchorA, newConnection);

        var evB = new UEPConnected(anchorB, anchorB.Comp, newConnection);
        RaiseLocalEvent(anchorB, ref evB, true);
        OnConnectionChanged(anchorB, newConnection);
    }

    private void OnConnectionSeverance(Entity<UEPConnectionComponent> connection, Entity<UEPComponent> anchorA, Entity<UEPComponent> anchorB)
    {
        var evA = new UEPDisconnected(anchorA, anchorA.Comp);
        RaiseLocalEvent(anchorA, ref evA, true);
        OnConnectionChanged(anchorA, null, connection);

        var evB = new UEPDisconnected(anchorB, anchorB.Comp);
        RaiseLocalEvent(anchorB, ref evB, true);
        OnConnectionChanged(anchorB, null, connection);
    }

    private void OnConnectionChanged(Entity<UEPComponent> entity, Entity<UEPConnectionComponent>? newConnection = null, Entity<UEPConnectionComponent>? previousConnection = null)
    {
        UpdateUserInterface(entity.Owner, entity.Comp);

        var ev = new UEPConnectionChange(entity, entity.Comp, previousConnection, newConnection);
        RaiseLocalEvent(entity, ref ev, true);
    }

    public void Disconnect(Entity<UEPConnectionComponent> entity)
    {

        if (TryComp<UEPComponent>(entity.Comp.AnchorA, out var uepA))
            ResetUep((entity.Comp.AnchorA, uepA));
        if (TryComp<UEPComponent>(entity.Comp.AnchorB, out var uepB))
            ResetUep((entity.Comp.AnchorB, uepB));

        if (uepA != null && uepB != null) // Which should always be the case...
            OnConnectionSeverance(entity, (entity.Comp.AnchorA, uepA), (entity.Comp.AnchorB, uepB));
        QueueDel(entity);
    }

    public void Disconnect(Entity<UEPComponent> entity)
    {
        var connectionEntity = entity.Comp.Connection.GetValueOrDefault();
        if (connectionEntity == default || TerminatingOrDeleted(connectionEntity))
        {
            ResetUep(entity);
        }
        else if (TryComp<UEPConnectionComponent>(connectionEntity, out var connection))
            Disconnect((connectionEntity, connection));
    }

    public bool CheckConnection(EntityUid entity)
    {
        if (TryComp<UEPComponent>(entity, out var uep))
            return CheckConnection((entity, uep));
        if (TryComp<UEPConnectionComponent>(entity, out var conn))
            return CheckConnection((entity, conn));
        return false;
    }

    public bool CheckConnection(Entity<UEPComponent> entity)
    {
        var result = IsConnected(entity) && CanKeepConnection(entity);
        if (!result)
            Disconnect(entity);
        return result;
    }

    public bool CheckConnection(Entity<UEPConnectionComponent> entity)
    {
        // Probably spawning, skip.
        if (!entity.Comp.AnchorA.Valid || !entity.Comp.AnchorA.Valid) return true;

        var result = CanKeepConnection(entity);
        if (!result)
            Disconnect(entity);
        return result;
    }

    private IEnumerable<Entity<UEPComponent>> GetAvailableConnections(Entity<UEPComponent> entity)
    {
        var enumerator = EntityQueryEnumerator<UEPComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);
            if (uid == entity.Owner) continue;
            if (CanKeepConnection(entity, ent))
                yield return ent;
        }
    }
}
