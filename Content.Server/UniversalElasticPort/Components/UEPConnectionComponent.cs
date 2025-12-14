using Content.Server.UniversalElasticPort.Systems;

namespace Content.Server.UniversalElasticPort.Components;

[RegisterComponent, Access(typeof(UniversalElasticPortSystem))]
public sealed partial class UEPConnectionComponent : Component
{
    [DataField]
    public EntityUid AnchorA { get; set; }
    [DataField]
    public EntityUid AnchorB { get; set; }

    // Runtime only.
    public TimeSpan NextUpdate;
}
