using CobbsClasses.src.Renderers;

using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace CobbsClasses.src;

public class CobbsClassesModSystem : ModSystem
{
    public override double ExecuteOrder() => 0.6;

    private DarkvisionRenderer? darkvisionRenderer;

    public override void StartClientSide(ICoreClientAPI api)
    {
        darkvisionRenderer = new(api);
        api.Event.RegisterRenderer(darkvisionRenderer, EnumRenderStage.Before, "cobbsclasses-darkvision");
    }

    public override void Dispose()
    {
        darkvisionRenderer?.Dispose();
        base.Dispose();
    }
}
