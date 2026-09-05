using System.Text.Json.Serialization;

namespace AlchemyStars.Engine;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(WorkspaceDocument))]
[JsonSerializable(typeof(AppPreferenceData))]
internal sealed partial class WorkspaceJsonContext : JsonSerializerContext;
