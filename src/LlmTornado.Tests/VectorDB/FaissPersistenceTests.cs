using System.Reflection;
using LlmTornado.VectorDatabases.Faiss;

namespace LlmTornado.Tests.VectorDB;

[TestFixture]
public class FaissPersistenceTests
{
    [Test]
    public void MetadataFile_ReloadsNextIdAndEntries()
    {
        // The shape SaveIndex writes.
        Dictionary<string, object> written = new()
        {
            ["nextId"] = 2L,
            ["entries"] = new Dictionary<long, FaissEntry>
            {
                [0] = new("a", "first", new Dictionary<string, object> { ["k"] = "v" }),
                [1] = new("b", "second")
            }
        };
        string json = System.Text.Json.JsonSerializer.Serialize(written, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        MethodInfo read = typeof(FaissCollectionClient).GetMethod("ReadMetadata", BindingFlags.NonPublic | BindingFlags.Static)!;
        object? loaded = read.Invoke(null, [json]);

        Assert.That(loaded, Is.Not.Null);
        Type type = loaded!.GetType();
        Assert.That(type.GetProperty("NextId")!.GetValue(loaded), Is.EqualTo(2L));
        Dictionary<long, FaissEntry> entries = (Dictionary<long, FaissEntry>)type.GetProperty("Entries")!.GetValue(loaded)!;
        Assert.That(entries.Keys, Is.EquivalentTo(new long[] { 0, 1 }));
        Assert.That(entries[0].Id, Is.EqualTo("a"));
        Assert.That(entries[1].Document, Is.EqualTo("second"));
    }
}
