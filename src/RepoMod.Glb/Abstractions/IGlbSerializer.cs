using RepoMod.Glb.Contracts;

namespace RepoMod.Glb.Abstractions;

public interface IGlbSerializer
{
    GlbBuildResult Build(GlbCompositionResult composition);
}
