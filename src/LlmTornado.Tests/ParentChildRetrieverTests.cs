using LlmTornado.VectorDatabases;

namespace LlmTornado.Tests;

[TestFixture]
public class ParentChildRetrieverTests
{
    [Test]
    public void Search_ReturnsTheParentsOfTheMatchingChunks()
    {
        ParentChildDocumentRetriever retriever = Build();

        List<Document> found = retriever.Search([0f]).ToList();

        Assert.That(found.Select(d => d.Id), Is.EqualTo(new[] { "parent-1" }));
    }

    [Test]
    public async Task SearchAsync_ReturnsTheParentsOfTheMatchingChunks()
    {
        ParentChildDocumentRetriever retriever = Build();

        List<Document> found = (await retriever.SearchAsync([0f])).ToList();

        Assert.That(found.Select(d => d.Id), Is.EqualTo(new[] { "parent-1" }));
    }

    private static ParentChildDocumentRetriever Build()
    {
        MemoryDocumentStore docStore = new MemoryDocumentStore("docs");
        docStore.SetDocument(new Document("parent-1", "the whole section"));

        return new ParentChildDocumentRetriever(new ChunkOnlyVectorDatabase("docs"), docStore);
    }

    /// <summary>
    /// Answers every query with one chunk whose id is not a parent id, the way a real store
    /// returns chunk ids and records the parent in the metadata.
    /// </summary>
    private class ChunkOnlyVectorDatabase(string collection) : IVectorDatabase
    {
        private static readonly VectorDocument[] Chunks =
        [
            new VectorDocument("chunk-1", "part of the section", new Dictionary<string, object> { ["parent_id"] = "parent-1" })
        ];

        public string GetCollectionName() => collection;
        public VectorDocument[] QueryByEmbedding(float[] embedding, TornadoWhereOperator? where = null, int topK = 5, bool includeScore = true) => Chunks;
        public Task<VectorDocument[]> QueryByEmbeddingAsync(float[] embedding, TornadoWhereOperator? where = null, int topK = 5, bool includeScore = true) => Task.FromResult(Chunks);

        public void AddDocuments(VectorDocument[] documents) => throw new NotSupportedException();
        public VectorDocument[]? GetDocuments(string[] ids) => throw new NotSupportedException();
        public void UpsertDocuments(VectorDocument[] documents) => throw new NotSupportedException();
        public void UpdateDocuments(VectorDocument[] documents) => throw new NotSupportedException();
        public void DeleteDocuments(string[] ids) => throw new NotSupportedException();
        public Task AddDocumentsAsync(VectorDocument[] documents) => throw new NotSupportedException();
        public Task<VectorDocument[]> GetDocumentsAsync(string[] ids) => throw new NotSupportedException();
        public Task UpdateDocumentsAsync(VectorDocument[] documents) => throw new NotSupportedException();
        public Task UpsertDocumentsAsync(VectorDocument[] documents) => throw new NotSupportedException();
        public Task DeleteDocumentsAsync(string[] ids) => throw new NotSupportedException();
        public Task DeleteAllDocumentsAsync() => throw new NotSupportedException();
    }
}
