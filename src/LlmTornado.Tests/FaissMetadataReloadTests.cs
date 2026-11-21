using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using LlmTornado.VectorDatabases.Faiss;

namespace LlmTornado.Tests;

[TestFixture]
public class FaissMetadataReloadTests
{
    private static readonly BindingFlags NonPublic = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Dictionary<string, object> ReloadMetadata()
    {
        // Same shape SaveIndex writes to the metadata file.
        string json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["nextId"] = 1L,
            ["entries"] = new Dictionary<long, FaissEntry>
            {
                [0] = new("a", "doc", new Dictionary<string, object> { ["page"] = 3, ["lang"] = "en", ["draft"] = true, ["score"] = 1.5 })
            }
        });

        object file = typeof(FaissCollectionClient).GetMethod("ReadMetadata", NonPublic)!.Invoke(null, [json])!;
        Dictionary<long, FaissEntry> entries = (Dictionary<long, FaissEntry>)file.GetType().GetProperty("Entries")!.GetValue(file)!;
        return entries[0].Metadata!;
    }

    private static bool Matches(Dictionary<string, object> metadata, Dictionary<string, object> filter)
    {
        // The constructor needs the native FAISS library; MatchesFilter does not touch instance state.
        object client = RuntimeHelpers.GetUninitializedObject(typeof(FaissCollectionClient));
        return (bool)typeof(FaissCollectionClient).GetMethod("MatchesFilter", NonPublic)!.Invoke(client, [metadata, filter])!;
    }

    [Test]
    public void DirectEquality_MatchesAfterReloadFromDisk()
    {
        Dictionary<string, object> metadata = ReloadMetadata();

        Assert.That(Matches(metadata, new() { ["page"] = 3 }), Is.True);
        Assert.That(Matches(metadata, new() { ["page"] = 4 }), Is.False);
        Assert.That(Matches(metadata, new() { ["lang"] = "en" }), Is.True);
        Assert.That(Matches(metadata, new() { ["draft"] = true }), Is.True);
        Assert.That(Matches(metadata, new() { ["score"] = 1.5 }), Is.True);
    }

    [Test]
    public void Operators_MatchAfterReloadFromDisk()
    {
        Dictionary<string, object> metadata = ReloadMetadata();

        Assert.That(Matches(metadata, new() { ["page"] = new Dictionary<string, object> { ["$eq"] = 3 } }), Is.True);
        Assert.That(Matches(metadata, new() { ["page"] = new Dictionary<string, object> { ["$gt"] = 2 } }), Is.True);
        Assert.That(Matches(metadata, new() { ["page"] = new Dictionary<string, object> { ["$in"] = new object[] { 3, 5 } } }), Is.True);
        Assert.That(Matches(metadata, new() { ["page"] = new Dictionary<string, object> { ["$nin"] = new object[] { 3, 5 } } }), Is.False);
        Assert.That(Matches(metadata, new() { ["lang"] = new Dictionary<string, object> { ["$ne"] = "en" } }), Is.False);
    }
}
