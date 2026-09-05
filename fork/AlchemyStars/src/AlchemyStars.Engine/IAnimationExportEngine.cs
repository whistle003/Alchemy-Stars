namespace AlchemyStars.Engine;

public interface IAnimationExportEngine
{
    EngineCapabilities Capabilities { get; }

    AnimationExportResult Export(AnimationExportRequest request);
}
