using ChromaDB.Client;
using ChromaDB.Client.Models;
using System.Collections;


namespace LlmTornado.VectorDatabases.Intergrations
{
    public class TornadoChromaWhere
    {
        public TornadoWhereOperator? TornadoWhereOperator { get; set; }
        public TornadoChromaWhere(TornadoWhereOperator where)
        {
            TornadoWhereOperator = where;
        }

        internal ChromaWhereOperator? ToChromaWhere()
        {
            Dictionary<string, object>? where = TornadoWhereOperator?.ToWhere();
            return where is null ? null : ChromaWhereOperator.FromDictionary(where);
        }
    }

    public class TornadoChromaDB : IVectorDatabase
    {
        public ChromaClient ChromaClient { get; set; }
        public ChromaCollection ChromaCollection { get; set; }

        public ChromaCollectionClient CollectionClient { get; set; }
        public string CollectionName { get; set; } = "testCollection";

        private ChromaConfigurationOptions _configOptions { get; set; }

        public TornadoChromaDB(string uri) : this(new ChromaConfigurationOptions(uri: uri))
        {
        }

        /// <summary>
        /// Connects with the given options, for example the tenant, database and API key of Chroma Cloud.
        /// </summary>
        public TornadoChromaDB(ChromaConfigurationOptions options)
        {
            _configOptions = options;
            ChromaClient = new ChromaClient(_configOptions);
            Task.Run(async () => await TestChromaConnection()).Wait();
        }

        private async Task TestChromaConnection()
        {
            try
            {
                string testCollectionName = $"test_collection_{Guid.NewGuid().ToString().Substring(0, 4)}";
                await InitializeCollection(testCollectionName);
                await DeleteCollectionAsync(testCollectionName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"ChromaDB instance not reachable at {_configOptions.Uri}", ex);
            }
        }

        public async Task InitializeCollection(string collectionName)
        {
            if (collectionName.Equals(CollectionName))
            {
                return;
            }
            CollectionName = collectionName;
            ChromaCollection = await ChromaClient.GetOrCreateCollectionAsync(collectionName);
            CollectionClient = ChromaClient.GetCollectionClient(ChromaCollection);
        }

        public void AddDocuments(VectorDocument[] documents)
        {
            Task.Run(async () => await AddDocumentsAsync(documents)).Wait();
        }

        public async Task AddDocumentsAsync(VectorDocument[] documents)
        {
            ThrowIfCollectionNotInitialized();
            List<string> ids = new List<string>();
            List<ReadOnlyMemory<float>> embeddings = new List<ReadOnlyMemory<float>>();
            List<Dictionary<string, object>> metadatas = new List<Dictionary<string, object>>();
            List<string> contents = new List<string>();

            foreach (var doc in documents)
            {
                ids.Add(doc.Id);
                embeddings.Add(new(doc.Embedding ?? Array.Empty<float>()));
                metadatas.Add(ToChromaMetadata(doc.Metadata));
                contents.Add(doc.Content ?? "");
            }

            await CollectionClient.AddAsync(ids, embeddings: embeddings, metadatas: metadatas, documents: contents);
        }

        public void DeleteCollection(string collectionName)
        {
            Task.Run(async () => await DeleteCollectionAsync(collectionName)).Wait();
        }

        public async Task DeleteCollectionAsync(string collectionName)
        {
            await ChromaClient.DeleteCollectionAsync(collectionName);
            if(CollectionName == collectionName)
            {
                CollectionClient = null;
                ChromaCollection = null;
            }
        }
        private void ThrowIfCollectionNotInitialized()
        {
            if(CollectionClient == null)
            {
                throw new InvalidOperationException("CollectionClient is not initialized. Please initialize the collection first.");
            }
        }
        public void DeleteDocuments(string[] ids)
        {
            Task.Run(async () => await DeleteDocumentsAsync(ids)).Wait();
        }

        public async Task DeleteDocumentsAsync(string[] ids)
        {
            ThrowIfCollectionNotInitialized();
            await CollectionClient.DeleteAsync(ids.ToList());
        }

        public VectorDocument[]? GetDocuments(string[] ids)
        {
            return Task.Run(async () => await GetDocumentsAsync(ids)).Result;
        }

        public async Task<VectorDocument[]> GetDocumentsAsync(string[] ids)
        {
            ThrowIfCollectionNotInitialized();
            var docs = await CollectionClient.GetAsync(ids.ToList(), include: ChromaGetInclude.Documents | ChromaGetInclude.Metadatas | ChromaGetInclude.Embeddings );
            return docs.Select(d => new VectorDocument(d.Id, d.Document ?? "", ToMetadata(d.Metadata), d.Embedding?.ToArray() ?? Array.Empty<float>())).ToArray();
        }

        public VectorDocument[] QueryByEmbedding(float[] embedding, TornadoWhereOperator where = null, int topK = 5, bool includeScore = true)
        {
            return Task.Run(async () => await QueryByEmbeddingAsync(embedding, where, topK, includeScore)).Result;
        }

        public async Task<VectorDocument[]> QueryByEmbeddingAsync(float[] embedding, TornadoWhereOperator where = null, int topK = 5, bool includeScore = true)
        {
            ThrowIfCollectionNotInitialized();
            List<VectorDocument> results = new List<VectorDocument>();
            TornadoChromaWhere tornadoChromaWhere = new TornadoChromaWhere(where);
            var queryData = await CollectionClient.QueryAsync(
                [new(embedding)],
                nResults: topK,
                where: tornadoChromaWhere.ToChromaWhere(),
                include: ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances | ChromaQueryInclude.Documents);

            foreach (var item in queryData)
            {
                foreach (var entry in item)
                {
                    float[]? _embedding = entry.Embedding.HasValue ? entry.Embedding.Value.ToArray() : Array.Empty<float>();
                    results.Add(new VectorDocument(entry.Id, entry.Document ?? "", ToMetadata(entry.Metadata), _embedding, entry.Distance));
                }
            }
            return results.ToArray();
        }

        public void UpdateDocuments(VectorDocument[] documents)
        {
            Task.Run(async () => await UpdateDocumentsAsync(documents)).Wait();
        }

        public async Task UpdateDocumentsAsync(VectorDocument[] documents)
        {
            ThrowIfCollectionNotInitialized();

            // Chroma keeps the stored embedding unless one is sent, so documents with a new embedding go in their own batch
            foreach (bool hasEmbedding in new[] { true, false })
            {
                VectorDocument[] batch = documents.Where(d => (d.Embedding is { Length: > 0 }) == hasEmbedding).ToArray();
                if (batch.Length == 0)
                {
                    continue;
                }

                await CollectionClient.UpdateAsync(
                    batch.Select(d => d.Id).ToList(),
                    embeddings: hasEmbedding ? batch.Select(d => new ReadOnlyMemory<float>(d.Embedding!)).ToList() : null,
                    metadatas: batch.Select(d => ToChromaMetadata(d.Metadata)).ToList(),
                    documents: batch.Select(d => d.Content ?? "").ToList()
                    );
            }
        }

        public void UpsertDocuments(VectorDocument[] documents)
        {
            Task.Run(async () => await UpsertDocumentsAsync(documents)).Wait();
        }

        public async Task UpsertDocumentsAsync(VectorDocument[] documents)
        {
            ThrowIfCollectionNotInitialized();
            await CollectionClient.UpsertAsync(
                documents.Select(d => d.Id).ToList(),
                embeddings: documents.Select(d => new ReadOnlyMemory<float>(d.Embedding ?? Array.Empty<float>())).ToList(),
                metadatas: documents.Select(d => ToChromaMetadata(d.Metadata)).ToList(),
                documents: documents.Select(d => d.Content ?? "").ToList()
                );
        }

        public async Task DeleteAllDocumentsAsync()
        {
            ThrowIfCollectionNotInitialized();

            // Chroma rejects a delete without ids or filters: read a page of ids and delete it, from offset 0 as the deleted records leave
            while (true)
            {
                var records = await CollectionClient.GetAsync(limit: 1000, offset: 0, include: ChromaGetInclude.None);
                if (records.Count == 0)
                {
                    break;
                }
                await CollectionClient.DeleteAsync(records.Select(r => r.Id).ToList());
            }
        }

        public async Task<VectorDocument[]> GetDocumentWhereAsync(TornadoWhereOperator where)
        {
            ThrowIfCollectionNotInitialized();

            var docs = await CollectionClient.GetAsync(
                where: new TornadoChromaWhere(where).ToChromaWhere(),
                include: ChromaGetInclude.Documents | ChromaGetInclude.Metadatas);

            return docs.Select(d => new VectorDocument(d.Id, d.Document ?? "", ToMetadata(d.Metadata))).ToArray();
        }

        public string GetCollectionName() => CollectionName;

        private static Dictionary<string, object>? ToMetadata(IReadOnlyDictionary<string, object>? metadata)
            => metadata?.ToDictionary(m => m.Key, m => m.Value);

        // No metadata, or an empty one, is null for that record: Chroma takes a metadata with at least one key, and an update keeps the stored one.
        private static Dictionary<string, object> ToChromaMetadata(Dictionary<string, object>? metadata)
            => metadata is { Count: > 0 } ? metadata : null!;
    }
}
