using RepoMod.Parser.Implementation;

namespace RepoMod.Parser.UnitTests;

public class ModParserTests
{
    [Fact]
    public void ExtractMetadata_InferHeadSlotFromFileNameSuffix()
    {
        var parser = new ModParser();

        var result = parser.ExtractMetadata("CoolHat_head.hhh");

        Assert.Equal("CoolHat_head", result.Name);
        Assert.Equal("head", result.SlotTag);
    }

    [Fact]
    public void ExtractMetadata_ReturnsUnknownWhenNoSuffixFound()
    {
        var parser = new ModParser();

        var result = parser.ExtractMetadata("NoSlot.hhh");

        Assert.Equal("unknown", result.SlotTag);
    }
}
