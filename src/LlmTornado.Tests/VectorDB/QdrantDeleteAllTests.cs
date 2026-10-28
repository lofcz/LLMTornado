using System.IO;
using LlmTornado.VectorDatabases;
using LlmTornado.VectorDatabases.Qdrant;

namespace LlmTornado.Tests.VectorDB;

[TestFixture]
public class QdrantDeleteAllTests
{
    private static readonly float[] Query = [1f, 0f, 0f];

    private static VectorDocument[] Documents() =>
    [
        new VectorDocument("a", "first") { Embedding = [1f, 0f, 0f] },
        new VectorDocument("b", "second") { Embedding = [0f, 1f, 0f] }
    ];

    [Test]
    [Explicit("Requires a Qdrant server on localhost:6334")]
    public async Task Qdrant_DeleteAllDocuments_EmptiesTheCollection()
    {
        QdrantVectorDatabase db = new QdrantVectorDatabase("localhost", port: 6334, vectorDimension: 3);
        await db.InitializeCollectionAsync($"delete-all-{Guid.NewGuid():N}");

        try
        {
            await db.UpsertDocumentsAsync(Documents().Select(d => { d.Id = Guid.NewGuid().ToString(); return d; }).ToArray());
            Assert.That((await db.QueryByEmbeddingAsync(Query, topK: 5)).Length, Is.EqualTo(2));

            await db.DeleteAllDocumentsAsync();

            Assert.That(await db.QueryByEmbeddingAsync(Query, topK: 5), Is.Empty);
        }
        finally
        {
            await db.DeleteCollectionAsync(db.GetCollectionName());
        }
    }
}
