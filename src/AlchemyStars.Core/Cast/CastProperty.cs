using System.Globalization;

namespace AlchemyStars.Core.Cast;

public sealed class CastProperty
{
    private static readonly IReadOnlyDictionary<string, (int ElementSize, int Components)> Layouts =
        new Dictionary<string, (int, int)>(StringComparer.Ordinal)
        {
            ["b"] = (1, 1),
            ["h"] = (2, 1),
            ["i"] = (4, 1),
            ["l"] = (8, 1),
            ["f"] = (4, 1),
            ["d"] = (8, 1),
            ["s"] = (0, 1),
            ["2v"] = (8, 2),
            ["3v"] = (12, 3),
            ["4v"] = (16, 4),
        };

    public CastProperty(string name, string type, object values)
    {
        if (!Layouts.ContainsKey(type))
        {
            throw new InvalidDataException($"不支持的 CAST 属性类型：{type}");
        }

        Name = name;
        Type = type;
        Values = values;
        ValidateValueType();
    }

    public string Name { get; }
    public string Type { get; }
    public object Values { get; set; }

    public int Components => Layouts[Type].Components;
    public int ElementSize => Layouts[Type].ElementSize;

    public int Count => Values switch
    {
        string => 1,
        byte[] values => values.Length,
        ushort[] values => values.Length,
        uint[] values => values.Length,
        ulong[] values => values.Length,
        float[] values => values.Length / Components,
        double[] values => values.Length,
        _ => throw new InvalidDataException($"属性 {Name} 的值类型无效。"),
    };

    public string GetString() => Values as string
        ?? throw new InvalidDataException($"属性 {Name} 不是字符串。类型={Type}");

    public byte[] GetBytes() => Values as byte[]
        ?? throw new InvalidDataException($"属性 {Name} 不是 byte 数组。类型={Type}");

    public ushort[] GetUInt16s() => Values as ushort[]
        ?? throw new InvalidDataException($"属性 {Name} 不是 ushort 数组。类型={Type}");

    public uint[] GetUInt32s() => Values as uint[]
        ?? throw new InvalidDataException($"属性 {Name} 不是 uint 数组。类型={Type}");

    public ulong[] GetUInt64s() => Values as ulong[]
        ?? throw new InvalidDataException($"属性 {Name} 不是 ulong 数组。类型={Type}");

    public float[] GetFloats() => Values as float[]
        ?? throw new InvalidDataException($"属性 {Name} 不是 float 数组。类型={Type}");

    public double[] GetDoubles() => Values as double[]
        ?? throw new InvalidDataException($"属性 {Name} 不是 double 数组。类型={Type}");

    public IReadOnlyList<int> GetFrameIndices()
    {
        return Type switch
        {
            "b" => GetBytes().Select(static x => (int)x).ToArray(),
            "h" => GetUInt16s().Select(static x => (int)x).ToArray(),
            "i" => GetUInt32s().Select(static x => checked((int)x)).ToArray(),
            _ => throw new InvalidDataException($"属性 {Name} 不能作为帧索引读取。类型={Type}"),
        };
    }

    public CastProperty Clone() => new(Name, Type, Values switch
    {
        string value => value,
        byte[] values => values.ToArray(),
        ushort[] values => values.ToArray(),
        uint[] values => values.ToArray(),
        ulong[] values => values.ToArray(),
        float[] values => values.ToArray(),
        double[] values => values.ToArray(),
        _ => throw new InvalidDataException($"属性 {Name} 的值类型无效。"),
    });

    public int GetSerializedLength()
    {
        var nameLength = System.Text.Encoding.UTF8.GetByteCount(Name);
        var valueLength = Type == "s"
            ? System.Text.Encoding.UTF8.GetByteCount(GetString()) + 1
            : checked(ElementSize * Count);
        return checked(8 + nameLength + valueLength);
    }

    public override string ToString() => $"{Name}:{Type}[{Count.ToString(CultureInfo.InvariantCulture)}]";

    private void ValidateValueType()
    {
        var valid = Type switch
        {
            "b" => Values is byte[],
            "h" => Values is ushort[],
            "i" => Values is uint[],
            "l" => Values is ulong[],
            "f" or "2v" or "3v" or "4v" => Values is float[],
            "d" => Values is double[],
            "s" => Values is string,
            _ => false,
        };

        if (!valid)
        {
            throw new InvalidDataException($"CAST 属性 {Name} 的 .NET 值类型与 {Type} 不匹配。实际：{Values.GetType().Name}");
        }

        if (Values is float[] floats && floats.Length % Components != 0)
        {
            throw new InvalidDataException($"CAST 属性 {Name} 的分量数不能被 {Components} 整除。");
        }
    }
}

