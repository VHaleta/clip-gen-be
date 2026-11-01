using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipGen.Be.IntegrationTests.Infrastructure;

/// <summary>Те же настройки JSON, что у Host (строковые enum'ы) — иначе DTO с enum-полями не round-trip'ятся.</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
