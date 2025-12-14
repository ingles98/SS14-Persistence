using Content.Server.UniversalElasticPort.Systems;
using Robust.Shared.Utility;

namespace Content.Server.UniversalElasticPort.Components;

[RegisterComponent, Access(typeof(UniversalElasticPortSystem))]
public sealed partial class UEPComponent : Component
{
    [DataField, ViewVariables]
    public EntityUid? Connection = null;

    [DataField, ViewVariables]
    public float MaxRange = 4f;

    [DataField]
    public Dictionary<string, bool> EnabledPlugs = new();

    [DataField, ViewVariables, AutoNetworkedField]
    public SpriteSpecifier? LinkSprite;

}

[ByRefEvent]
public readonly struct UEPConnected(
    EntityUid entity,
    UEPComponent component,
    Entity<UEPConnectionComponent> connection)
{
    public EntityUid Entity { get; } = entity;
    public readonly UEPComponent Component = component;
    public readonly Entity<UEPConnectionComponent> NewConnection = connection;
}

[ByRefEvent]
public readonly struct UEPDisconnected(
    EntityUid entity,
    UEPComponent component)
{
    public EntityUid Entity { get; } = entity;
    public readonly UEPComponent Component = component;
}

[ByRefEvent]
public readonly struct UEPConnectionChange(
    EntityUid entity,
    UEPComponent component,
    Entity<UEPConnectionComponent>? prevConn,
    Entity<UEPConnectionComponent>? newConn)
{
    public EntityUid Entity { get; } = entity;
    public readonly UEPComponent Component = component;
    public readonly Entity<UEPConnectionComponent>? PreviousConnection = prevConn;
    public readonly Entity<UEPConnectionComponent>? NewConnection = newConn;
}
