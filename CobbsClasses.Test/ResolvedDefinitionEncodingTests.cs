using CobbsClasses.Source.Compatibility.Requirements;

using Newtonsoft.Json.Linq;

using Vintagestory.API.Datastructures;

namespace CobbsClasses.Tests;

public sealed class ResolvedDefinitionEncodingTests
{
    private static JObject Payload() => JObject.Parse("""{"traits":[{"code":"test","attributeDescriptions":{"a":"quoted \"text\" — café"}}],"classes":[]}""");

    [Test]
    public void EncodedDefinitionsSurviveGameWorldConfigJson()
    {
        JObject payload = Payload();
        TreeAttribute tree = new();
        tree.SetString("cobbsclasses:resolvedClassDefinitions", ResolvedDefinitionEncoding.Encode(payload));
        JObject serialized = JObject.Parse(tree.ToJsonToken());
        string stored = (string)serialized["cobbsclasses:resolvedClassDefinitions"]!;
        Assert.That(JToken.DeepEquals(payload, ResolvedDefinitionEncoding.Decode(stored)), Is.True);
    }

    [Test]
    public void EncodedDefinitionsSurviveGameBinarySerialization()
    {
        JObject payload = Payload();
        TreeAttribute tree = new();
        tree.SetString("definitions", ResolvedDefinitionEncoding.Encode(payload));
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true))
        {
            tree.ToBytes(writer);
        }

        stream.Position = 0;
        TreeAttribute restored = new();
        using BinaryReader reader = new(stream);
        restored.FromBytes(reader);
        Assert.That(JToken.DeepEquals(payload, ResolvedDefinitionEncoding.Decode(restored.GetString("definitions"))), Is.True);
    }

    [Test]
    public void LegacyRawJsonRemainsReadable()
    {
        JObject payload = Payload();
        Assert.That(JToken.DeepEquals(payload, ResolvedDefinitionEncoding.Decode(payload.ToString())), Is.True);
    }

    [Test]
    public void MalformedBase64IsRejected()
    {
        Assert.Throws<FormatException>(() => ResolvedDefinitionEncoding.Decode("base64:!invalid!"));
    }
}