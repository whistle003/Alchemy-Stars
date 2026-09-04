namespace Alchemist.UI;

internal static class PartOrdering
{
    public static IReadOnlyList<Part> ForSkeletonMerge(IEnumerable<Part> parts) =>
        parts.OrderBy(static part => part.Type switch
        {
            PartType.ViewHands => 0,
            PartType.Weapon => 1,
            PartType.Attachment => 2,
            _ => 3,
        }).ToArray();
}
