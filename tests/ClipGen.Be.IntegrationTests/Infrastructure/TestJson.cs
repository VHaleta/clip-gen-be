using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipGen.Be.IntegrationTests.Infrastructure;

/// <summary>Same JSON settings as the Host (string enums); otherwise DTOs with enum fields do not round-trip.</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
