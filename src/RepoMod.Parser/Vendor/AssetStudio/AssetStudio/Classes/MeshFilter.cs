namespace AssetStudio
{
    public sealed class MeshFilter : Component
    {
        public PPtr<Object> m_Mesh;

        public MeshFilter(ObjectReader reader) : base(reader)
        {
            m_Mesh = new PPtr<Object>(reader);
        }
    }
}
