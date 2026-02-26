using RepoMod.Glb.Contracts;
using RepoMod.Parser.Contracts;

namespace RepoMod.Glb.Abstractions;

public interface IGlbCompositionPlanner
{
    GlbCompositionPlan BuildPlan(
        ConverterScene avatarScene,
        IReadOnlyList<GlbCosmeticSelection> selections,
        GlbCompositionOptions? options = null);
}
