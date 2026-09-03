namespace AlchemyStars.Core.Cast;

public sealed class CastDocument
{
    public uint Version { get; set; } = CastConstants.Version;
    public uint Flags { get; set; }
    public List<CastNode> Roots { get; } = [];

    public IEnumerable<CastNode> Nodes() => Roots.SelectMany(static root => root.DescendantsAndSelf());

    public IEnumerable<CastNode> NodesOfType(uint identifier) => Nodes().Where(x => x.Identifier == identifier);

    public CastDocument CloneDeep()
    {
        var clone = new CastDocument { Version = Version, Flags = Flags };
        clone.Roots.AddRange(Roots.Select(static x => x.CloneDeep()));
        return clone;
    }
}

