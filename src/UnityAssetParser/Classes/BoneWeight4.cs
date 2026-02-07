namespace UnityAssetParser.Classes;

/// <summary>
/// Per-vertex bone weights (up to 4 influences).
/// </summary>
public sealed class BoneWeight4
{
    public int BoneIndex0 { get; set; }
    public int BoneIndex1 { get; set; }
    public int BoneIndex2 { get; set; }
    public int BoneIndex3 { get; set; }

    public float Weight0 { get; set; }
    public float Weight1 { get; set; }
    public float Weight2 { get; set; }
    public float Weight3 { get; set; }
}
