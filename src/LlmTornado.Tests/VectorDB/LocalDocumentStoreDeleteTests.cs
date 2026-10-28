using System.IO;
using LlmTornado.VectorDatabases;

namespace LlmTornado.Tests.VectorDB;

[TestFixture]
public class LocalDocumentStoreDeleteTests
{
    private string directory = null!;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"tornado-delete-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(directory, true);
    }

    [Test]
    public void LocalDocumentStore_DeleteDocument_RemovesOnlyThatDocument()
    {
        LocalDocumentStore store = new LocalDocumentStore(directory, "docs");
        store.SetDocument(new Document("a", "first"));
        store.SetDocument(new Document("b", "second"));

        store.DeleteDocument("a");

        Assert.That(store.GetAllDocuments().Select(d => d.Id), Is.EqualTo(new[] { "b" }));
    }

    [Test]
    public void LocalDocumentStore_DeleteDocument_UnknownId_Throws()
    {
        LocalDocumentStore store = new LocalDocumentStore(directory, "docs");

        Assert.Throws<KeyNotFoundException>(() => store.DeleteDocument("missing"));
    }
}
