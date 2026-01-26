namespace UnityAssetParser.Classes;

/// <summary>
/// Vertex channel format enumeration for Unity version &lt; 2017.
/// This is a verbatim port from UnityPy/enums/VertexFormat.py.
/// 
/// Defines the data type of vertex channel components.
/// 
/// Reference: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/enums/VertexFormat.py
/// </summary>
public enum VertexChannelFormat : byte
{
    Float = 0,
    Float16 = 1,
    Color = 2,
    Byte = 3,
    UInt32 = 4
}

/// <summary>
/// Vertex format enumeration for Unity version 2017.
/// This is a verbatim port from UnityPy/enums/VertexFormat.py.
/// 
/// Defines the data type of vertex channel components in Unity 2017.x.
/// 
/// Reference: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/enums/VertexFormat.py
/// </summary>
public enum VertexFormat2017 : byte
{
    Float = 0,
    Float16 = 1,
    Color = 2,
    UNorm8 = 3,
    SNorm8 = 4,
    UNorm16 = 5,
    SNorm16 = 6,
    UInt8 = 7,
    SInt8 = 8,
    UInt16 = 9,
    SInt16 = 10,
    UInt32 = 11,
    SInt32 = 12
}

/// <summary>
/// Vertex format enumeration for Unity version >= 2019.
/// This is a verbatim port from UnityPy/enums/VertexFormat.py.
/// 
/// Defines the data type of vertex channel components in Unity 2019+.
/// Note: Color format was removed in 2019.
/// 
/// Reference: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/enums/VertexFormat.py
/// </summary>
public enum VertexFormat : byte
{
    Float = 0,
    Float16 = 1,
    UNorm8 = 2,
    SNorm8 = 3,
    UNorm16 = 4,
    SNorm16 = 5,
    UInt8 = 6,
    SInt8 = 7,
    UInt16 = 8,
    SInt16 = 9,
    UInt32 = 10,
    SInt32 = 11
}
