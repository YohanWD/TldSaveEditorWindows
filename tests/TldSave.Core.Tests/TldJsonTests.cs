using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TldSave.Core.Tests;

public class TldJsonTests
{
    [Theory]
    // The game writes non-standard NaN / Infinity: they must come back as bare symbols, not "NaN" strings
    [InlineData("{\"a\":NaN,\"b\":Infinity,\"c\":-Infinity}")]
    // Date-like text must stay text, not become a DateTime with another format
    [InlineData("{\"m_Timestamp\":\"2022-12-05 22:17:29\"}")]
    // Integers beyond int / long range
    [InlineData("{\"x\":2000000000,\"y\":-2147483648,\"z\":12345678901234567890}")]
    // Numbers keep their exact text
    [InlineData("{\"f\":0.824081957,\"g\":7752.188,\"h\":1.0,\"i\":1E-05}")]
    public void Parse_then_serialize_keeps_the_exact_text(string json)
    {
        Assert.Equal(json, TldJson.Serialize(TldJson.Parse(json)));
    }

    [Fact]
    public void Nesting_up_to_the_limit_is_accepted()
    {
        TldJson.Parse(new string('[', 200) + new string(']', 200));
    }

    [Theory]
    [InlineData(300)]
    [InlineData(100_000)]
    public void Absurdly_deep_nesting_is_rejected(int depth)
    {
        Assert.Throws<JsonReaderException>(() => TldJson.Parse(new string('[', depth) + new string(']', depth)));
    }

    [Fact]
    public void ReadNested_and_WriteNested_round_trip_a_json_string_field()
    {
        var obj = new JObject { ["m_Thirst_Serialized"] = "{\"m_CurrentThirstProxy\":32.5}" };

        var thirst = (JObject)obj.ReadNested("m_Thirst_Serialized")!;
        Assert.Equal(32.5, (double)thirst["m_CurrentThirstProxy"]!);

        thirst["m_CurrentThirstProxy"] = 10.0;
        obj.WriteNested("m_Thirst_Serialized", thirst);
        Assert.Equal("{\"m_CurrentThirstProxy\":10.0}", (string)obj["m_Thirst_Serialized"]!);
    }

    [Fact]
    public void ReadNested_returns_null_for_missing_or_empty_fields()
    {
        var obj = new JObject { ["empty"] = "" };
        Assert.Null(obj.ReadNested("empty"));
        Assert.Null(obj.ReadNested("missing"));
    }

    [Fact]
    public void Expand_parses_nested_json_strings_recursively()
    {
        var obj = new JObject { ["outer"] = "{\"inner\":\"[1,2]\"}", ["text"] = "not json" };
        var expanded = TldJson.Expand(obj);
        Assert.Equal(2, ((JArray)expanded["outer"]!["inner"]!).Count);
        Assert.Equal("not json", (string)expanded["text"]!);
    }
}
