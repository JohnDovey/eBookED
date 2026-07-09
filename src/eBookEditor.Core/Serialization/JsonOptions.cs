using System.Text.Json;

namespace eBookEditor.Core.Serialization;

public static class JsonOptions
{
    public static JsonSerializerOptions Create() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new DateOnlyJsonConverter() }
    };
}
