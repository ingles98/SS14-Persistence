using Content.Shared.Roles;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int>
        GridClaimPersonalMaxTiles = CVarDef.Create("gridconfig.claim_personal_max_tiles", 150, CVar.ARCHIVE);
}