using Cast.NET;

namespace Alchemist.UI;

internal static class CastNodeTraversal
{
    public static IEnumerable<CastNode> DescendantsAndSelf(CastNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in DescendantsAndSelf(child))
                yield return descendant;
    }
}
