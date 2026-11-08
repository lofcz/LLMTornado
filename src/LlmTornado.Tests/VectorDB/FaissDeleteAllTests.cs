using System.IO;
using LlmTornado.VectorDatabases;
using LlmTornado.VectorDatabases.Faiss.Integrations;

namespace LlmTornado.Tests.VectorDB;

[TestFixture]
public class FaissDeleteAllTests
{
    [Test]
    public void DeleteAllDocuments_WithoutCollection_ThrowsInvalidOperationException()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            FaissVectorDatabase db = new(directory, vectorDimension: 3);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await db.DeleteAllDocumentsAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    [Platform("Win", Reason = "FaissNet 1.1.0 ships native libraries for Windows only")]
    public async Task DeleteAllDocuments_ClearsAndPersistsCollection_AndAllowsNewDocuments()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        FaissVectorDatabase db = new(directory, vectorDimension: 3);
        FaissVectorDatabase? reopened = null;
        try
        {
            await db.InitializeCollection("delete-all");
            await db.AddDocumentsAsync([
                new VectorDocument("a", "first") { Embedding = [1f, 0f, 0f] },
                new VectorDocument("b", "second") { Embedding = [0f, 1f, 0f] }
            ]);
            Assert.That(await db.CollectionClient!.CountAsync(), Is.EqualTo(2));

            await db.DeleteAllDocumentsAsync();

            Assert.That(await db.CollectionClient.CountAsync(), Is.Zero);
            Assert.That(await db.GetDocumentsAsync(["a", "b"]), Is.Empty);
            Assert.That(await db.QueryByEmbeddingAsync([1f, 0f, 0f]), Is.Empty);
            Assert.That(await db.FaissClient.GetCollectionAsync("delete-all"), Is.Not.Null);

            // Reload before disposing the original client: deletion must persist immediately.
            reopened = new(directory, vectorDimension: 3);
            await reopened.InitializeCollection("delete-all");
            Assert.That(await reopened.CollectionClient!.CountAsync(), Is.Zero);
            Assert.That(await reopened.QueryByEmbeddingAsync([1f, 0f, 0f]), Is.Empty);
            reopened.CollectionClient.Dispose();
            reopened = null;

            await db.DeleteAllDocumentsAsync();
            await db.AddDocumentsAsync([new VectorDocument("c", "third") { Embedding = [0f, 0f, 1f] }]);
            Assert.That((await db.QueryByEmbeddingAsync([0f, 0f, 1f])).Select(d => d.Id), Is.EqualTo(new[] { "c" }));
        }
        finally
        {
            reopened?.CollectionClient?.Dispose();
            db.CollectionClient?.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }
}
