namespace AlchemyStars.Core.Cast;

public sealed class CastNode
{
    public CastNode(uint identifier, ulong hash)
    {
        Identifier = identifier;
        Hash = hash;
    }

    public uint Identifier { get; set; }
    public ulong Hash { get; set; }
    public List<CastProperty> Properties { get; } = [];
    public List<CastNode> Children { get; } = [];

    public CastProperty? Property(string name) =>
        Properties.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

    public string? StringProperty(string name) => Property(name)?.GetString();

    public void SetProperty(CastProperty value)
    {
        var index = Properties.FindIndex(x => string.Equals(x.Name, value.Name, StringComparison.Ordinal));
        if (index >= 0)
        {
            Properties[index] = value;
        }
        else
        {
            Properties.Add(value);
        }
    }

    public IEnumerable<CastNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var descendant in child.DescendantsAndSelf())
            {
                yield return descendant;
            }
        }
    }

    public IEnumerable<CastNode> ChildrenOfType(uint identifier) =>
        Children.Where(x => x.Identifier == identifier);

    public CastNode? ChildOfType(uint identifier) =>
        Children.FirstOrDefault(x => x.Identifier == identifier);

    public CastNode CloneDeep()
    {
        var clone = new CastNode(Identifier, Hash);
        clone.Properties.AddRange(Properties.Select(static x => x.Clone()));
        clone.Children.AddRange(Children.Select(static x => x.CloneDeep()));
        return clone;
    }

    public int GetSerializedLength() => checked(
        24
        + Properties.Sum(static x => x.GetSerializedLength())
        + Children.Sum(static x => x.GetSerializedLength()));

    public override string ToString() => $"{CastConstants.ToFourCc(Identifier)} 0x{Hash:X16}";
}

