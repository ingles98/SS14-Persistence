using Content.Client.UniversalElasticPort.UI;
using Content.Shared.UniversalElasticPort.BUIStates;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.UniversalElasticPort.BUI;

[UsedImplicitly]
public sealed class UEPBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private UEPConsoleWindow? _window;

    public UEPBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<UEPConsoleWindow>();
        _window.BUI = this;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not UEPBoundUserInterfaceState cState)
            return;

        _window?.UpdateState(cState);
    }
}
