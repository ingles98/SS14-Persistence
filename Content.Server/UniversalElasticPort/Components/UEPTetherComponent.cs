using Content.Server.UniversalElasticPort.Systems;

namespace Content.Server.UniversalElasticPort.Components;

[RegisterComponent, Access(typeof(UniversalElasticPortSystem))]
public sealed partial class UEPTetherComponent : Component
{
    [DataField]
    public EntityUid Connection;

    [DataField]
    public string NodeIdentifier;
}
