using System.Text.Json;
using AlchemyStars.Core.Baking;

return await Cli.RunAsync(args);

internal static class Cli
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase))
        {
            PrintHelp();
            return Task.FromResult(0);
        }

        try
        {
            var command = args[0].StartsWith("--", StringComparison.Ordinal) ? "bake" : args[0].ToLowerInvariant();
            var offset = command == "bake" && args[0].StartsWith("--", StringComparison.Ordinal) ? 0 : 1;
            var options = ParseOptions(args[offset..]);
            var baker = new AlchemyStarsBaker();

            if (command == "analyze")
            {
                var report = baker.Analyze(
                    Required(options, "arms"),
                    Required(options, "weapon"),
                    Required(options, "animation"),
                    Optional(options, "additive"));
                Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
                return Task.FromResult(0);
            }

            if (command != "bake")
            {
                throw new ArgumentException($"未知命令：{command}");
            }

            var output = Required(options, "output");
            var request = new BakeRequest
            {
                ArmsModelPath = Required(options, "arms"),
                WeaponModelPath = Required(options, "weapon"),
                BaseAnimationPath = Required(options, "animation"),
                AdditiveAnimationPath = Optional(options, "additive"),
                OutputPath = output,
                AnimationName = Optional(options, "name") ?? Path.GetFileNameWithoutExtension(output),
                EnableLeftHandIk = !options.ContainsKey("no-left-ik"),
                EnableRightHandIk = !options.ContainsKey("no-right-ik"),
            };

            var lastProgress = -1;
            var progress = new Progress<int>(value =>
            {
                if (value == lastProgress)
                {
                    return;
                }

                lastProgress = value;
                Console.Error.Write($"\r烘焙进度 {value,3}%");
                if (value == 100)
                {
                    Console.Error.WriteLine();
                }
            });
            var bakeReport = baker.Bake(request, progress);
            Console.WriteLine(JsonSerializer.Serialize(bakeReport, JsonOptions));
            return Task.FromResult(0);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Alchemy Stars 失败：{exception.Message}");
            return Task.FromResult(1);
        }
    }

    private static Dictionary<string, string?> ParseOptions(string[] args)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"参数必须以 -- 开头：{token}");
            }

            var key = token[2..];
            if (key is "no-left-ik" or "no-right-ik")
            {
                result[key] = null;
                continue;
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"参数 --{key} 缺少值。");
            }

            result[key] = args[++i];
        }

        return result;
    }

    private static string Required(IReadOnlyDictionary<string, string?> options, string name) =>
        Optional(options, name) ?? throw new ArgumentException($"缺少必需参数 --{name}。");

    private static string? Optional(IReadOnlyDictionary<string, string?> options, string name) =>
        options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            Alchemy Stars — CAST 模型/动画合并与烘焙工具

            分析：
              AlchemyStars.Cli analyze --arms <手臂.cast> --weapon <武器.cast> \
                --animation <主动画.cast> [--additive <偏移.cast>]

            烘焙：
              AlchemyStars.Cli bake --arms <手臂.cast> --weapon <武器.cast> \
                --animation <主动画.cast> [--additive <偏移.cast>] \
                --output <输出.cast> [--name <动画名>] [--no-left-ik] [--no-right-ik]

            输出始终包含模型根和恰好一个绝对模式动画，可直接用 Cast Maya 插件导入。
            """);
    }
}

