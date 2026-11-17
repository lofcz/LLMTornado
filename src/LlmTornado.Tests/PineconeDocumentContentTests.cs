using System.Reflection;
using LlmTornado.VectorDatabases.Pinecone;
using Pinecone;

namespace LlmTornado.Tests;

[TestFixture]
public class PineconeDocumentContentTests
{
    [Test]
    public void VectorMetadata_RoundTripsTheDocumentWithoutIntegratedEmbedding()
    {
        Metadata stored = Build(new Dictionary<string, object> { ["topic"] = "cats" }, "Cats sleep most of the day.");
        Dictionary<string, object> metadata = Read(stored);

        string? document = Extract(metadata, null);

        Assert.That(document, Is.EqualTo("Cats sleep most of the day."));
        Assert.That(metadata, Is.EqualTo(new Dictionary<string, object> { ["topic"] = "cats" }));
    }

    [Test]
    public void VectorMetadata_WithoutDocument_StoresOnlyTheUserMetadata()
    {
        Metadata stored = Build(new Dictionary<string, object> { ["topic"] = "cats" }, "");

        Assert.That(stored.Keys, Is.EquivalentTo(new[] { "topic" }));
        Assert.That(Extract(Read(stored), null), Is.Null);
    }

    [Test]
    public void ExtractDocument_WithIntegratedEmbedding_ReadsTheRecordField()
    {
        Dictionary<string, object> metadata = new Dictionary<string, object> { ["text"] = "Dogs bark.", ["topic"] = "dogs" };

        string? document = Extract(metadata, "text");

        Assert.That(document, Is.EqualTo("Dogs bark."));
        Assert.That(metadata.Keys, Is.EquivalentTo(new[] { "text", "topic" }));
    }

    [Test]
    public void ExtractDocument_VectorUpsertedIntoAnIntegratedIndex_ReadsTheStoredDocument()
    {
        Metadata stored = Build(new Dictionary<string, object> { ["topic"] = "cats" }, "Cats purr.");
        Dictionary<string, object> metadata = Read(stored);

        string? document = Extract(metadata, "text");

        Assert.That(document, Is.EqualTo("Cats purr."));
        Assert.That(metadata.Keys, Is.EquivalentTo(new[] { "topic" }));
    }

    // Same conversion GetAsync and QueryAsync apply to the metadata of a returned vector
    private static Dictionary<string, object> Read(Metadata stored)
    {
        Dictionary<string, object> metadata = new Dictionary<string, object>();

        foreach (KeyValuePair<string, MetadataValue?> kvp in stored)
        {
            if (kvp.Value != null)
            {
                metadata[kvp.Key] = kvp.Value.Value;
            }
        }

        return metadata;
    }

    private static Metadata Build(Dictionary<string, object> metadata, string? document)
    {
        MethodInfo method = typeof(PineconeCollectionClient).GetMethod("BuildVectorMetadata", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Metadata)method.Invoke(null, [metadata, document])!;
    }

    private static string? Extract(Dictionary<string, object> metadata, string? recordField)
    {
        MethodInfo method = typeof(PineconeCollectionClient).GetMethod("ExtractDocument", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string?)method.Invoke(null, [metadata, recordField]);
    }
}
