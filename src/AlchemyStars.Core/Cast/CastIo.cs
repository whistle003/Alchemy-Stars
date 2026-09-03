using System.Text;

namespace AlchemyStars.Core.Cast;

public static class CastIo
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static CastDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, StrictUtf8, leaveOpen: false);

        if (reader.ReadUInt32() != CastConstants.Magic)
        {
            throw new InvalidDataException($"不是有效的 CAST 文件：{path}");
        }

        var document = new CastDocument
        {
            Version = reader.ReadUInt32(),
        };
        var rootCount = reader.ReadUInt32();
        document.Flags = reader.ReadUInt32();

        if (document.Version != CastConstants.Version)
        {
            throw new InvalidDataException($"仅支持 CAST v1，文件版本为 {document.Version}：{path}");
        }

        for (var i = 0U; i < rootCount; i++)
        {
            document.Roots.Add(ReadNode(reader));
        }

        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException($"CAST 文件尾部存在 {stream.Length - stream.Position} 个未解析字节：{path}");
        }

        return document;
    }

    public static void Save(CastDocument document, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("输出路径没有有效目录。");
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream, StrictUtf8, leaveOpen: false))
            {
                writer.Write(CastConstants.Magic);
                writer.Write(document.Version);
                writer.Write(checked((uint)document.Roots.Count));
                writer.Write(document.Flags);
                foreach (var root in document.Roots)
                {
                    WriteNode(writer, root);
                }
            }

            _ = Load(tempPath);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static CastNode ReadNode(BinaryReader reader)
    {
        var start = reader.BaseStream.Position;
        var identifier = reader.ReadUInt32();
        var declaredLength = reader.ReadUInt32();
        var hash = reader.ReadUInt64();
        var propertyCount = reader.ReadUInt32();
        var childCount = reader.ReadUInt32();

        if (declaredLength < 24)
        {
            throw new InvalidDataException($"CAST 节点长度无效：{declaredLength}");
        }

        var node = new CastNode(identifier, hash);
        for (var i = 0U; i < propertyCount; i++)
        {
            node.Properties.Add(ReadProperty(reader));
        }

        for (var i = 0U; i < childCount; i++)
        {
            node.Children.Add(ReadNode(reader));
        }

        var consumed = reader.BaseStream.Position - start;
        if (consumed != declaredLength)
        {
            throw new InvalidDataException(
                $"CAST 节点 {CastConstants.ToFourCc(identifier)} 长度不一致：声明 {declaredLength}，实际 {consumed}。");
        }

        return node;
    }

    private static CastProperty ReadProperty(BinaryReader reader)
    {
        var typeBytes = reader.ReadBytes(2);
        if (typeBytes.Length != 2)
        {
            throw new EndOfStreamException("CAST 属性类型读取不完整。");
        }

        var type = StrictUtf8.GetString(typeBytes).TrimEnd('\0');
        var nameLength = reader.ReadUInt16();
        var count = reader.ReadUInt32();
        var nameBytes = reader.ReadBytes(nameLength);
        if (nameBytes.Length != nameLength)
        {
            throw new EndOfStreamException("CAST 属性名读取不完整。");
        }

        var name = StrictUtf8.GetString(nameBytes);
        object values = type switch
        {
            "b" => reader.ReadBytes(checked((int)count)),
            "h" => ReadArray(count, reader.ReadUInt16),
            "i" => ReadArray(count, reader.ReadUInt32),
            "l" => ReadArray(count, reader.ReadUInt64),
            "f" => ReadArray(count, reader.ReadSingle),
            "d" => ReadArray(count, reader.ReadDouble),
            "s" => ReadNullTerminatedString(reader),
            "2v" => ReadArray(checked(count * 2), reader.ReadSingle),
            "3v" => ReadArray(checked(count * 3), reader.ReadSingle),
            "4v" => ReadArray(checked(count * 4), reader.ReadSingle),
            _ => throw new InvalidDataException($"属性 {name} 使用了未知 CAST 类型：{type}"),
        };

        if (values is byte[] bytes && bytes.Length != count)
        {
            throw new EndOfStreamException($"CAST 属性 {name} 的 byte 数组读取不完整。");
        }

        return new CastProperty(name, type, values);
    }

    private static T[] ReadArray<T>(uint count, Func<T> read)
    {
        var result = new T[checked((int)count)];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = read();
        }

        return result;
    }

    private static string ReadNullTerminatedString(BinaryReader reader)
    {
        using var buffer = new MemoryStream();
        while (true)
        {
            var value = reader.ReadByte();
            if (value == 0)
            {
                return StrictUtf8.GetString(buffer.ToArray());
            }

            buffer.WriteByte(value);
        }
    }

    private static void WriteNode(BinaryWriter writer, CastNode node)
    {
        writer.Write(node.Identifier);
        writer.Write(checked((uint)node.GetSerializedLength()));
        writer.Write(node.Hash);
        writer.Write(checked((uint)node.Properties.Count));
        writer.Write(checked((uint)node.Children.Count));

        foreach (var property in node.Properties)
        {
            WriteProperty(writer, property);
        }

        foreach (var child in node.Children)
        {
            WriteNode(writer, child);
        }
    }

    private static void WriteProperty(BinaryWriter writer, CastProperty property)
    {
        var typeBytes = StrictUtf8.GetBytes(property.Type);
        if (typeBytes.Length is < 1 or > 2)
        {
            throw new InvalidDataException($"CAST 属性类型必须为 1–2 个 UTF-8 字节：{property.Type}");
        }

        writer.Write(typeBytes);
        if (typeBytes.Length == 1)
        {
            writer.Write((byte)0);
        }

        var nameBytes = StrictUtf8.GetBytes(property.Name);
        writer.Write(checked((ushort)nameBytes.Length));
        writer.Write(checked((uint)property.Count));
        writer.Write(nameBytes);

        switch (property.Type)
        {
            case "b": writer.Write(property.GetBytes()); break;
            case "h": WriteArray(writer, property.GetUInt16s(), static (w, x) => w.Write(x)); break;
            case "i": WriteArray(writer, property.GetUInt32s(), static (w, x) => w.Write(x)); break;
            case "l": WriteArray(writer, property.GetUInt64s(), static (w, x) => w.Write(x)); break;
            case "f":
            case "2v":
            case "3v":
            case "4v": WriteArray(writer, property.GetFloats(), static (w, x) => w.Write(x)); break;
            case "d": WriteArray(writer, property.GetDoubles(), static (w, x) => w.Write(x)); break;
            case "s":
                writer.Write(StrictUtf8.GetBytes(property.GetString()));
                writer.Write((byte)0);
                break;
            default: throw new InvalidDataException($"不支持写入 CAST 属性类型：{property.Type}");
        }
    }

    private static void WriteArray<T>(BinaryWriter writer, IEnumerable<T> values, Action<BinaryWriter, T> write)
    {
        foreach (var value in values)
        {
            write(writer, value);
        }
    }
}

