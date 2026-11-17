using Pinecone;
using System.Linq;

namespace LlmTornado.VectorDatabases.Pinecone;

/// <summary>
/// Client for performing vector operations on a Pinecone namespace (collection).
/// </summary>
public class PineconeCollectionClient
{
    private readonly PineconeClient _client;
    private readonly PineconeVectorClient _vectorClient;
    private readonly PineconeConfigurationOptions _options;

    /// <summary>
    /// Gets the collection information.
    /// </summary>
    public PineconeCollection Collection { get; }

    /// <summary>
    /// Converts an object to MetadataValue using implicit conversions.
    /// MetadataValue supports: string, bool, double (and arrays/lists of these types).
    /// </summary>
    private static MetadataValue ConvertToMetadataValue(object value)
    {
        return value switch
        {
            string strVal => strVal,
            bool boolVal => boolVal,
            int intVal => (double)intVal,
            long longVal => (double)longVal,
            double doubleVal => doubleVal,
            float floatVal => (double)floatVal,
            _ => value?.ToString() ?? ""
        };
    }

    /// <summary>
    /// Metadata key holding the document text of a vector upserted without integrated embedding.
    /// </summary>
    private const string DocumentMetadataKey = "_document";

    // A plain vector has no record field, so the document text travels in the metadata under a reserved key
    private static Metadata BuildVectorMetadata(Dictionary<string, object> metadata, string? document)
    {
        Metadata result = new Metadata();

        foreach (KeyValuePair<string, object> kvp in metadata)
        {
            result[kvp.Key] = ConvertToMetadataValue(kvp.Value);
        }

        if (!string.IsNullOrEmpty(document))
        {
            result[DocumentMetadataKey] = document;
        }

        return result;
    }

    // Reads the document from the record field (integrated embedding) or from the reserved key, which is not user metadata and is removed
    private static string? ExtractDocument(Dictionary<string, object> metadata, string? recordField)
    {
        string? document = null;

        if (metadata.TryGetValue(DocumentMetadataKey, out object? stored))
        {
            document = stored?.ToString();
            metadata.Remove(DocumentMetadataKey);
        }

        if (recordField != null && metadata.TryGetValue(recordField, out object? docValue))
        {
            document = docValue?.ToString();
        }

        return document;
    }

    /// <summary>
    /// Creates a new PineconeCollectionClient instance.
    /// </summary>
    /// <param name="collection">The collection (namespace) information</param>
    /// <param name="client">The Pinecone client</param>
    /// <param name="vectorClient">The vector client for accessing embedding model</param>
    /// <param name="options">Configuration options</param>
    private static Metadata? BuildFilter(Dictionary<string, object>? where)
    {
        if (where is not { Count: > 0 })
        {
            return null;
        }

        Metadata filter = new Metadata();
        
        foreach (KeyValuePair<string, object> kvp in where)
        {
            filter[kvp.Key] = ConvertToFilterValue(kvp.Value);
        }

        return filter;
    }

    // A where clause nests operator objects ({ "$eq": value }) and lists ($and, $or, $in), which the plain metadata conversion would turn into the string form of the collection
    private static MetadataValue ConvertToFilterValue(object value)
    {
        switch (value)
        {
            case Dictionary<string, object> nested:
            {
                Metadata metadata = new Metadata();
                
                foreach (KeyValuePair<string, object> kvp in nested)
                {
                    metadata[kvp.Key] = ConvertToFilterValue(kvp.Value);
                }

                return metadata;
            }
            case string:
            {
                return ConvertToMetadataValue(value);
            }
            case System.Collections.IEnumerable items:
            {
                List<MetadataValue> list = [];
                
                foreach (object? item in items)
                {
                    list.Add(ConvertToFilterValue(item!));
                }

                return list;
            }
            default:
            {
                return ConvertToMetadataValue(value);
            }
        }
    }

    public PineconeCollectionClient(
        PineconeCollection collection, 
        PineconeClient client,
        PineconeVectorClient vectorClient,
        PineconeConfigurationOptions options)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _vectorClient = vectorClient ?? throw new ArgumentNullException(nameof(vectorClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the namespace for Records API operations (UpsertRecordsAsync).
    /// Records API requires "__default__" for the default namespace (not empty string).
    /// </summary>
    private string GetNamespaceForRecordsApi()
    {
        return string.IsNullOrEmpty(Collection.Name) ? "__default__" : Collection.Name;
    }

    /// <summary>
    /// Gets the namespace for Vectors API operations (Upsert, Fetch, Query, Delete).
    /// Vectors API uses empty string "" for the default namespace.
    /// </summary>
    private string GetNamespaceForVectorsApi()
    {
        return Collection.Name ?? "";
    }

    /// <summary>
    /// Generates embeddings for the given texts using Pinecone's inference API.
    /// </summary>
    private async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, string embeddingModel)
    {
        EmbedRequest embedRequest = new EmbedRequest
        {
            Model = embeddingModel,
            Inputs = texts.Select(text => new EmbedRequestInputsItem { Text = text }).ToList(),
            // Parameters are required by models - use custom if provided, otherwise use sensible defaults
            Parameters = (_options.EmbeddingParameters ?? new PineconeEmbeddingParameters()).ToDictionary()
        };
        
        EmbeddingsList response = await _client.Inference.EmbedAsync(embedRequest);
        
        // Convert response to List<float[]>
        // Embedding is a union type - extract DenseEmbedding and get Values
        return response.Data.Select(embedding =>
        {
            if (embedding.IsDense)
            {
                DenseEmbedding dense = embedding.AsDense();
                return dense.Values.ToArray();
            }

            throw new NotSupportedException("Sparse embeddings are not supported. Use dense embeddings.");
        }).ToList();
    }

    /// <summary>
    /// Adds vectors to the collection. Uses upsert internally.
    /// </summary>
    public async Task AddAsync(
        List<string> ids,
        List<float[]> embeddings,
        List<Dictionary<string, object>> metadatas,
        List<string> documents)
    {
        await UpsertAsync(ids, embeddings, metadatas, documents);
    }

    /// <summary>
    /// Upserts (inserts or updates) vectors in the collection.
    /// Uses integrated embedding if available, otherwise manual/auto-generated embeddings.
    /// </summary>
    public async Task UpsertAsync(
        List<string> ids,
        List<float[]> embeddings,
        List<Dictionary<string, object>> metadatas,
        List<string> documents)
    {
        if (ids.Count != embeddings.Count || ids.Count != metadatas.Count || ids.Count != documents.Count)
        {
            throw new ArgumentException("All lists must have the same length.");
        }

        // Check if index has integrated embedding
        bool hasIntegratedEmbedding = await _vectorClient.HasIntegratedEmbeddingAsync();
        bool needsEmbeddings = embeddings.Any(e => e == null || e.Length == 0);
        
        if (hasIntegratedEmbedding && needsEmbeddings)
        {
            // Mode 1: Use integrated embedding (Pinecone auto-generates embeddings from text)
            await UpsertWithIntegratedEmbeddingAsync(ids, metadatas, documents);
        }
        else
        {
            // Mode 2: Traditional vector upsert with explicit embeddings
            await UpsertWithVectorsAsync(ids, embeddings, metadatas, documents);
        }
    }

    /// <summary>
    /// Upserts using Pinecone's integrated embedding.
    /// The index automatically generates embeddings from the text in the record field.
    /// Uses the UpsertRecordsAsync API for indexes with integrated embedding.
    /// </summary>
    private async Task UpsertWithIntegratedEmbeddingAsync(
        List<string> ids,
        List<Dictionary<string, object>> metadatas,
        List<string> documents)
    {
        // Get the record field name from index configuration
        string? recordField = await _vectorClient.GetRecordFieldNameAsync();
        if (recordField == null)
        {
            throw new InvalidOperationException(
                "Index has integrated embedding but no record field configured in FieldMap.");
        }

        List<UpsertRecord> records = [];
        
        for (int i = 0; i < ids.Count; i++)
        {
            UpsertRecord record = new UpsertRecord
            {
                Id = ids[i]
            };
            
            // Add document text in the record field (Pinecone will auto-embed this)
            if (!string.IsNullOrEmpty(documents[i]))
            {
                record.AdditionalProperties[recordField] = documents[i];
            }
            
            // Add user-provided metadata as additional properties
            foreach (KeyValuePair<string, object> kvp in metadatas[i])
            {
                record.AdditionalProperties[kvp.Key] = kvp.Value;
            }

            records.Add(record);
        }

        // Use UpsertRecordsAsync for integrated embedding indexes
        // Records API requires "__default__" for default namespace (not empty string)
        await _client.Index(Collection.IndexName).UpsertRecordsAsync(GetNamespaceForRecordsApi(), records);
    }

    /// <summary>
    /// Upserts using traditional vector approach with explicit embeddings.
    /// Auto-generates embeddings via inference API if not provided and model is configured.
    /// </summary>
    private async Task UpsertWithVectorsAsync(
        List<string> ids,
        List<float[]> embeddings,
        List<Dictionary<string, object>> metadatas,
        List<string> documents)
    {
        // Check if we need to auto-generate embeddings
        bool needsEmbeddings = embeddings.Any(e => e == null || e.Length == 0);
        
        if (needsEmbeddings)
        {
            string? model = await _vectorClient.GetEmbeddingModelAsync();
            if (model == null)
            {
                throw new InvalidOperationException(
                    "Embeddings not provided and no embedding model configured. " +
                    "Set EmbeddingModel in PineconeConfigurationOptions or provide embeddings explicitly.");
            }
            
            embeddings = await GenerateEmbeddingsAsync(documents, model);
        }

        // Validate embeddings dimension
        for (int i = 0; i < embeddings.Count; i++)
        {
            if (embeddings[i] == null || embeddings[i].Length == 0)
            {
                throw new ArgumentException($"Embedding at index {i} is null or empty. Vector ID: {ids[i]}");
            }
            
            if (embeddings[i].Length != Collection.Dimension)
            {
                throw new ArgumentException(
                    $"Embedding dimension mismatch at index {i}. " +
                    $"Expected: {Collection.Dimension}, Got: {embeddings[i].Length}, Vector ID: {ids[i]}");
            }
        }

        List<Vector> vectors = [];
        
        for (int i = 0; i < ids.Count; i++)
        {
            // User-provided metadata plus the document text, so it can be read back
            Metadata metadata = BuildVectorMetadata(metadatas[i], documents[i]);

            vectors.Add(new Vector
            {
                Id = ids[i],
                Values = new ReadOnlyMemory<float>(embeddings[i]),
                Metadata = metadata
            });
        }

        UpsertRequest upsertRequest = new UpsertRequest
        {
            Vectors = vectors,
            Namespace = GetNamespaceForVectorsApi()
        };

        await _client.Index(Collection.IndexName).UpsertAsync(upsertRequest);
    }

    /// <summary>
    /// Updates existing vectors in the collection. Uses upsert internally.
    /// </summary>
    public async Task UpdateAsync(
        List<string> ids,
        List<float[]> embeddings,
        List<Dictionary<string, object>> metadatas,
        List<string> documents)
    {
        await UpsertAsync(ids, embeddings, metadatas, documents);
    }

    /// <summary>
    /// Fetches vectors by their IDs.
    /// </summary>
    public async Task<List<PineconeEntry>> GetAsync(string[] ids)
    {
        FetchRequest fetchRequest = new FetchRequest
        {
            Ids = ids.ToList(),
            Namespace = GetNamespaceForVectorsApi()
        };

        FetchResponse? fetchResponse = await _client.Index(Collection.IndexName).FetchAsync(fetchRequest);
        List<PineconeEntry> entries = [];

        if (fetchResponse?.Vectors == null)
        {
            return entries;
        }

        foreach (KeyValuePair<string, Vector> kvp in fetchResponse.Vectors)
        {
            Vector vector = kvp.Value;
            Dictionary<string, object> metadata = new Dictionary<string, object>();
            
            if (vector.Metadata != null)
            {
                foreach (KeyValuePair<string, MetadataValue?> m in vector.Metadata)
                {
                    // MetadataValue is a OneOf type - extract the underlying value
                    if (m.Value != null)
                    {
                        metadata[m.Key] = m.Value.Value;
                    }
                }
            }
            
            // Document comes from the record field if integrated embedding is used, otherwise from the reserved metadata key
            string? recordField = await _vectorClient.GetRecordFieldNameAsync();
            string? document = ExtractDocument(metadata, recordField);

            float[]? embedding = null;
            if (vector.Values.HasValue)
            {
                embedding = vector.Values.Value.ToArray();
            }

            entries.Add(new PineconeEntry(
                id: kvp.Key,
                document: document,
                metadata: metadata,
                embedding: embedding
            ));
        }

        return entries;
    }

    /// <summary>
    /// Queries the collection for similar vectors.
    /// </summary>
    public async Task<List<PineconeEntry>> QueryAsync(
        float[] queryEmbedding,
        int topK = 10,
        Dictionary<string, object>? whereMetadata = null)
    {
        Metadata? filter = BuildFilter(whereMetadata);

        QueryRequest queryRequest = new QueryRequest
        {
            Vector = new ReadOnlyMemory<float>(queryEmbedding),
            TopK = (uint)topK,
            Filter = filter,
            IncludeMetadata = true,
            IncludeValues = false,
            Namespace = GetNamespaceForVectorsApi()
        };

        QueryResponse? queryResponse = await _client.Index(Collection.IndexName).QueryAsync(queryRequest);

        List<PineconeEntry> entries = [];

        if (queryResponse?.Matches == null)
        {
            return entries;
        }

        foreach (ScoredVector match in queryResponse.Matches)
        {
            Dictionary<string, object> metadata = new Dictionary<string, object>();
            
            if (match.Metadata != null)
            {
                foreach (KeyValuePair<string, MetadataValue?> m in match.Metadata)
                {
                    // MetadataValue is a OneOf type - extract the underlying value
                    if (m.Value != null)
                    {
                        metadata[m.Key] = m.Value.Value;
                    }
                }
            }
            
            // Document comes from the record field if integrated embedding is used, otherwise from the reserved metadata key
            string? recordField = await _vectorClient.GetRecordFieldNameAsync();
            string? document = ExtractDocument(metadata, recordField);

            float[]? embedding = null;
            if (match.Values is { Length: > 0 })
            {
                embedding = match.Values.Value.ToArray();
            }

            entries.Add(new PineconeEntry(
                id: match.Id,
                document: document,
                metadata: metadata,
                embedding: embedding,
                distance: match.Score
            ));
        }

        return entries;
    }

    /// <summary>
    /// Deletes vectors by their IDs.
    /// </summary>
    public async Task DeleteAsync(List<string> ids)
    {
        DeleteRequest deleteRequest = new DeleteRequest
        {
            Ids = ids,
            Namespace = GetNamespaceForVectorsApi()
        };

        await _client.Index(Collection.IndexName).DeleteAsync(deleteRequest);
    }

    /// <summary>
    /// Deletes all vectors in the namespace.
    /// </summary>
    public async Task DeleteAllAsync()
    {
        try
        {
            DeleteRequest deleteRequest = new DeleteRequest
            {
                DeleteAll = true,
                Namespace = GetNamespaceForVectorsApi()
            };

            await _client.Index(Collection.IndexName).DeleteAsync(deleteRequest);
        }
        catch (PineconeApiException ex)
        {
            if (ex.StatusCode is 5)
            {
                // there are no rows to delete, we can ignore that
                // grpc: NOT_FOUND
                return;
            }

            throw;
        }
    }
}
