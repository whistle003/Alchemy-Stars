using Cast.NET;
using RedFox.Graphics3D.Skeletal;
using RedFox.Graphics3D.Translation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Alchemist.UI;

internal static class MayaCastPackage
{
    private const ulong HashBase = 0x534E495752545250;

    public static void Save(
        string outputPath,
        IEnumerable<Part> parts,
        SkeletonAnimation animation,
        Graphics3DTranslatorFactory translatorFactory)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullOutputPath)
            ?? throw new InvalidOperationException("Output path has no directory.");
        Directory.CreateDirectory(directory);

        var sources = PartOrdering.ForSkeletonMerge(parts)
            .Select(static part => part.FilePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sources.Any(path => string.Equals(path, fullOutputPath, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Output CAST cannot overwrite one of its source model files.");

        var animationPath = Path.Combine(directory, $".{Guid.NewGuid():N}.animation.cast");
        var packagePath = Path.Combine(directory, $".{Guid.NewGuid():N}.package.cast");
        try
        {
            translatorFactory.Save(animationPath, animation);
            var roots = new List<CastNode>();
            foreach (var modelPath in sources)
            {
                var modelCast = CastReader.Load(modelPath);
                foreach (var root in modelCast.RootNodes)
                {
                    RemoveNodes(root, CastNodeIdentifier.Animation);
                    if (DescendantsAndSelf(root).Any(static x => x.Identifier == CastNodeIdentifier.Model))
                    {
                        roots.Add(root);
                    }
                }
            }

            foreach (var root in CastReader.Load(animationPath).RootNodes)
            {
                RemoveNodes(root, CastNodeIdentifier.Model);
                if (DescendantsAndSelf(root).Any(static x => x.Identifier == CastNodeIdentifier.Animation))
                {
                    roots.Add(root);
                }
            }

            var package = new Cast.NET.Cast(roots);
            FreshenHashes(package);
            Validate(package);
            CastWriter.Save(packagePath, package);
            Validate(CastReader.Load(packagePath));
            File.Move(packagePath, fullOutputPath, overwrite: true);
        }
        finally
        {
            DeleteIfPresent(animationPath);
            DeleteIfPresent(packagePath);
        }
    }

    private static void RemoveNodes(CastNode node, CastNodeIdentifier identifier)
    {
        node.Children.RemoveAll(x => x.Identifier == identifier);
        foreach (var child in node.Children)
        {
            RemoveNodes(child, identifier);
        }
    }

    private static IEnumerable<CastNode> DescendantsAndSelf(CastNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in DescendantsAndSelf(child))
            {
                yield return descendant;
            }
        }
    }

    private static void FreshenHashes(Cast.NET.Cast cast)
    {
        var nextHash = HashBase;
        foreach (var root in cast.RootNodes)
        {
            var nodes = DescendantsAndSelf(root).ToArray();
            var remap = new Dictionary<ulong, ulong>();
            foreach (var node in nodes)
            {
                var replacement = nextHash++;
                if (node.Hash != 0)
                {
                    remap.TryAdd(node.Hash, replacement);
                }
                node.Hash = replacement;
            }

            foreach (var property in nodes
                .SelectMany(static x => x.Properties.Values)
                .OfType<CastArrayProperty<ulong>>())
            {
                for (var i = 0; i < property.Values.Count; i++)
                {
                    if (remap.TryGetValue(property.Values[i], out var replacement))
                    {
                        property.Values[i] = replacement;
                    }
                }
            }
        }
    }

    private static void Validate(Cast.NET.Cast cast)
    {
        var nodes = cast.RootNodes.SelectMany(DescendantsAndSelf).ToArray();
        var animationCount = nodes.Count(static x => x.Identifier == CastNodeIdentifier.Animation);
        var modelCount = nodes.Count(static x => x.Identifier == CastNodeIdentifier.Model);
        if (animationCount != 1)
        {
            throw new InvalidDataException($"Maya CAST package must contain exactly one animation; found {animationCount}.");
        }
        if (modelCount < 2)
        {
            throw new InvalidDataException($"Maya CAST package must contain at least two models; found {modelCount}.");
        }
        if (nodes.Select(static x => x.Hash).Distinct().Count() != nodes.Length)
        {
            throw new InvalidDataException("Maya CAST package contains duplicate node hashes.");
        }
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
