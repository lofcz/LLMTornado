namespace LlmTornado.VectorDatabases.Faiss.Integrations;

/// <summary>
/// FAISS-specific where operator for metadata filtering.
/// </summary>
public class TornadoFaissWhere
{
    public TornadoWhereOperator? TornadoWhereOperator { get; set; }
    
    public TornadoFaissWhere(TornadoWhereOperator? where)
    {
        TornadoWhereOperator = where;
    }

    internal Dictionary<string, object>? ToWhere()
    {
        return TornadoWhereOperator?.ToWhere();
    }
}

/// <summary>
/// FAISS vector database implementation of IVectorDatabase.
/// Provides integration with FAISS for efficient similarity search and vector storage.
/// </summary>
public class FaissVectorDatabase : IVectorDatabase
{
    public FaissClient FaissClient { get; set; }
    public FaissCollection? FaissCollection { get; set; }
    public FaissCollectionClient? CollectionClient { get; set; }
    public string CollectionName { get; set; } = "defaultCollection";

    private FaissConfigurationOptions _configOptions { get; set; }
    private int _vectorDimension { get; set; }

    /// <summary>
    /// Initializes a new instance of the FaissVectorDatabase class.
    /// </summary>
    /// <param name="indexDirectory">Directory path where FAISS indexes will be stored. Defaults to "./faiss_indexes".</param>
    /// <param name="vectorDimension">Dimension of the vectors to be stored. Default is 1536.</param>
    /// <param name="skipConnectionTest">If true, skips the connection test during initialization. Default is false.</param>
    public FaissVectorDatabase(string? indexDirectory = null, int vectorDimension = 1536)
    {
        _vectorDimension = vectorDimension;
        _configOptions = new FaissConfigurationOptions(indexDirectory);
        FaissClient = new FaissClient(_configOptions);
    }

    private async Task TestFaissConnection()
    {
        try
        {
            await FaissClient.InitializeAsync();
            
            string testCollectionName = $"test_collection_{Guid.NewGuid().ToString().Substring(0, 4)}";
            await InitializeCollection(testCollectionName);
            await DeleteCollectionAsync(testCollectionName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"FAISS initialization failed", ex);
        }
    }

    /// <summary>
    /// Initializes a collection with the specified name.
    /// </summary>
    public async Task InitializeCollection(string collectionName)
    {
        if (collectionName.Equals(CollectionName) && CollectionClient != null)
        {
            return;
        }
        
        CollectionName = collectionName;
        FaissCollection = await FaissClient.GetOrCreateCollectionAsync(collectionName, _vectorDimension);
        CollectionClient = new FaissCollectionClient(FaissCollection, FaissClient);
    }

    /// <summary>
    /// Deletes a collection.
    /// </summary>
    public void DeleteCollection(string collectionName)
    {
        Task.Run(async () => await DeleteCollectionAsync(collectionName)).Wait();
    }

    /// <summary>
    /// Deletes a collection asynchronously.
    /// </summary>
    public async Task DeleteCollectionAsync(string collectionName)
    {
        if (CollectionClient != null && CollectionName == collectionName)
        {
            CollectionClient.Dispose();
            CollectionClient = null;
        }
        
        await FaissClient.DeleteCollectionAsync(collectionName);
        
        if (CollectionName == collectionName)
        {
            FaissCollection = null;
        }
    }

    private void ThrowIfCollectionNotInitialized()
    {
        if (CollectionClient == null)
        {
            throw new InvalidOperationException("CollectionClient is not initialized. Please initialize the collection first.");
        }
    }

    public string GetCollectionName() => CollectionName;

    public void AddDocuments(VectorDocument[] documents)
    {
        Task.Run(async () => await AddDocumentsAsync(documents)).Wait();
    }

    public async Task AddDocumentsAsync(VectorDocument[] documents)
    {
        ThrowIfCollectionNotInitialized();
        
        List<string> ids = new List<string>();
        List<float[]> embeddings = new List<float[]>();
        List<Dictionary<string, object>> metadatas = new List<Dictionary<string, object>>();
        List<string> contents = new List<string>();

        foreach (var doc in documents)
        {
            ids.Add(doc.Id);
            embeddings.Add(doc.Embedding ?? throw new ArgumentException($"Embedding is required for document '{doc.Id}'"));
            metadatas.Add(doc.Metadata ?? new Dictionary<string, object>());
            contents.Add(doc.Content ?? "");
        }

        await CollectionClient!.AddAsync(ids, embeddings: embeddings, metadatas: metadatas, documents: contents);
    }

    public VectorDocument[]? GetDocuments(string[] ids)
    {
        return Task.Run(async () => await GetDocumentsAsync(ids)).Result;
    }

    public async Task<VectorDocument[]> GetDocumentsAsync(string[] ids)
    {
        ThrowIfCollectionNotInitialized();
        var entries = await CollectionClient!.GetAsync(ids);
        return entries.Select(e => new VectorDocument(
            e.Id, 
            e.Document ?? "", 
            e.Metadata, 
            e.Embedding ?? Array.Empty<float>()
        )).ToArray();
    }

    public void UpsertDocuments(VectorDocument[] documents)
    {
        Task.Run(async () => await UpsertDocumentsAsync(documents)).Wait();
    }

    public async Task UpsertDocumentsAsync(VectorDocument[] documents)
    {
        ThrowIfCollectionNotInitialized();
        await CollectionClient!.UpsertAsync(
            documents.Select(d => d.Id).ToList(),
            embeddings: documents.Select(d => d.Embedding ?? throw new ArgumentException($"Embedding is required for document '{d.Id}'")).ToList(),
            metadatas: documents.Select(d => d.Metadata ?? new Dictionary<string, object>()).ToList(),
            documents: documents.Select(d => d.Content ?? "").ToList()
        );
    }

    public void UpdateDocuments(VectorDocument[] documents)
    {
        Task.Run(async () => await UpdateDocumentsAsync(documents)).Wait();
    }

    public async Task UpdateDocumentsAsync(VectorDocument[] documents)
    {
        ThrowIfCollectionNotInitialized();
        
        // Filter out null embeddings for update
        var nonNullEmbeddings = documents.Select(d => d.Embedding).Where(e => e != null).Cast<float[]>().ToList();
        
        await CollectionClient!.UpdateAsync(
            documents.Select(d => d.Id).ToList(),
            embeddings: nonNullEmbeddings.Count == documents.Length ? nonNullEmbeddings : null,
            metadatas: documents.Select(d => d.Metadata ?? new Dictionary<string, object>()).ToList(),
            documents: documents.Select(d => d.Content ?? "").ToList()
        );
    }

    public void DeleteDocuments(string[] ids)
    {
        Task.Run(async () => await DeleteDocumentsAsync(ids)).Wait();
    }

    public async Task DeleteDocumentsAsync(string[] ids)
    {
        ThrowIfCollectionNotInitialized();
        await CollectionClient!.DeleteAsync(ids.ToList());
    }

    public Task DeleteAllDocumentsAsync()
    {
        ThrowIfCollectionNotInitialized();
        return CollectionClient!.DeleteAllAsync();
    }

    public VectorDocument[] QueryByEmbedding(float[] embedding, TornadoWhereOperator? where = null, int topK = 5, bool includeScore = true)
    {
        return Task.Run(async () => await QueryByEmbeddingAsync(embedding, where, topK, includeScore)).Result;
    }

    public async Task<VectorDocument[]> QueryByEmbeddingAsync(float[] embedding, TornadoWhereOperator? where = null, int topK = 5, bool includeScore = true)
    {
        ThrowIfCollectionNotInitialized();
        
        Dictionary<string, object>? whereDict = null;
        if (where != null)
        {
            var tornadoFaissWhere = new TornadoFaissWhere(where);
            whereDict = tornadoFaissWhere.ToWhere();
        }

        var entries = await CollectionClient!.QueryAsync(embedding, topK, whereDict);

        List<VectorDocument> results = new List<VectorDocument>();
        foreach (var entry in entries)
        {
            float[]? entryEmbedding = includeScore ? (entry.Embedding ?? Array.Empty<float>()) : null;
            results.Add(new VectorDocument(
                entry.Id, 
                entry.Document ?? "", 
                entry.Metadata, 
                entryEmbedding,
                includeScore ? entry.Distance : null
            ));
        }

        return results.ToArray();
    }
}
