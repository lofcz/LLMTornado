using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;
using QdrantRange = Qdrant.Client.Grpc.Range;

namespace LlmTornado.VectorDatabases.Qdrant;

/// <summary>
/// Qdrant vector database implementation of IVectorDatabase.
/// This class provides integration with Qdrant for vector storage and similarity search.
/// </summary>
public class QdrantVectorDatabase : IVectorDatabase
{
    private readonly QdrantClient client;
    private string collectionName = "";
    private readonly int vectorDimension;

    /// <summary>
    /// Initializes a new instance of the QdrantVectorDatabase class.
    /// </summary>
    /// <param name="host">Qdrant server host (default: localhost)</param>
    /// <param name="port">Qdrant server port (default: 6334 for gRPC)</param>
    /// <param name="vectorDimension">The dimension of vectors to be stored (default: 1536)</param>
    /// <param name="https">Whether to use HTTPS (default: false)</param>
    /// <param name="apiKey">Optional API key for authentication</param>
    public QdrantVectorDatabase(
        string host = "localhost",
        int port = 6334,
        int vectorDimension = 1536,
        bool https = false,
        string? apiKey = null)
    {
        this.vectorDimension = vectorDimension;

        client = new QdrantClient(host, port, https: https, apiKey: apiKey);
    }

    /// <summary>
    /// Initializes or switches to a collection.
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    public async Task InitializeCollectionAsync(string collectionName)
    {
        this.collectionName = collectionName;
        
        // Check if collection exists
        IReadOnlyList<string> collections = await client.ListCollectionsAsync();
        bool exists = collections.Any(c => c == collectionName);

        if (!exists)
        {
            // Create collection with vector configuration
            await client.CreateCollectionAsync(
                collectionName,
                new VectorParams { Size = (ulong)vectorDimension, Distance = Distance.Cosine }
            );
        }
    }

    public async Task<List<VectorCollection>> GetCollectionList()
    {
        IReadOnlyList<string> collections = await client.ListCollectionsAsync();
        List<VectorCollection> result = [];
        foreach (string collection in collections)
        {
           result.Add(new VectorCollection(collection));
        }

        return result;
    }

    /// <summary>
    /// Deletes a collection from Qdrant.
    /// </summary>
    /// <param name="collectionName">Name of the collection to delete</param>
    public async Task DeleteCollectionAsync(string collectionName)
    {
        await client.DeleteCollectionAsync(collectionName);
        
        if (this.collectionName == collectionName)
        {
            this.collectionName = "";
        }
    }

    private void ThrowIfCollectionNotInitialized()
    {
        if (string.IsNullOrEmpty(collectionName))
        {
            throw new InvalidOperationException(
                "Collection is not initialized. Please call InitializeCollectionAsync first.");
        }
    }

    /// <summary>
    /// Gets the current collection name.
    /// </summary>
    public string GetCollectionName() => collectionName;

    /// <summary>
    /// Adds documents to the vector database.
    /// </summary>
    public void AddDocuments(VectorDocument[] documents)
    {
        Task.Run(async () => await AddDocumentsAsync(documents)).Wait();
    }

    /// <summary>
    /// Adds documents to the vector database asynchronously.
    /// </summary>
    public async Task AddDocumentsAsync(VectorDocument[] documents)
    {
        ThrowIfCollectionNotInitialized();

        List<PointStruct> points = [];
        
        foreach (VectorDocument doc in documents)
        {
            PointStruct point = new PointStruct
            {
                Id = new PointId { Uuid = doc.Id },
                Vectors = doc.Embedding ?? [],
                Payload = { }
            };

            // Add content to payload
            if (!string.IsNullOrEmpty(doc.Content))
            {
                point.Payload.Add("content", doc.Content);
            }

            // Add metadata to payload
            if (doc.Metadata != null)
            {
                foreach (KeyValuePair<string, object> kvp in doc.Metadata)
                {
                    point.Payload.Add(kvp.Key, ConvertToValue(kvp.Value));
                }
            }

            points.Add(point);
        }

        await client.UpsertAsync(collectionName, points);
    }

    /// <summary>
    /// Retrieves documents by their IDs.
    /// </summary>
    public VectorDocument[]? GetDocuments(string[] ids)
    {
        return Task.Run(async () => await GetDocumentsAsync(ids)).Result;
    }

    /// <summary>
    /// Retrieves documents by their IDs asynchronously.
    /// </summary>
    public async Task<VectorDocument[]> GetDocumentsAsync(string[] ids)
    {
        ThrowIfCollectionNotInitialized();

        List<PointId> pointIds = ids.Select(id => new PointId { Uuid = id }).ToList();
        IReadOnlyList<RetrievedPoint> points = await client.RetrieveAsync(
            collectionName,
            pointIds,
            true, // with payload
            true  // with vectors
        );

        return points.Select(ConvertToVectorDocument).ToArray();
    }

    /// <summary>
    /// Updates existing documents in the vector database.
    /// </summary>
    public void UpdateDocuments(VectorDocument[] documents)
    {
        Task.Run(async () => await UpdateDocumentsAsync(documents)).Wait();
    }

    /// <summary>
    /// Updates existing documents in the vector database asynchronously.
    /// </summary>
    public async Task UpdateDocumentsAsync(VectorDocument[] documents)
    {
        // Qdrant doesn't have a separate update operation - use upsert
        await UpsertDocumentsAsync(documents);
    }

    /// <summary>
    /// Inserts or updates documents in the vector database.
    /// </summary>
    public void UpsertDocuments(VectorDocument[] documents)
    {
        Task.Run(async () => await UpsertDocumentsAsync(documents)).Wait();
    }

    /// <summary>
    /// Inserts or updates documents in the vector database asynchronously.
    /// </summary>
    public async Task UpsertDocumentsAsync(VectorDocument[] documents)
    {
        ThrowIfCollectionNotInitialized();

        List<PointStruct> points = [];

        foreach (VectorDocument doc in documents)
        {
            PointStruct point = new PointStruct
            {
                Id = new PointId { Uuid = doc.Id },
                Vectors = doc.Embedding ?? [],
                Payload = { }
            };

            // Add content to payload
            if (!string.IsNullOrEmpty(doc.Content))
            {
                point.Payload.Add("content", doc.Content);
            }

            // Add metadata to payload
            if (doc.Metadata != null)
            {
                foreach (KeyValuePair<string, object> kvp in doc.Metadata)
                {
                    point.Payload.Add(kvp.Key, ConvertToValue(kvp.Value));
                }
            }

            points.Add(point);
        }

        await client.UpsertAsync(collectionName, points);
    }

    /// <summary>
    /// Deletes documents by their IDs.
    /// </summary>
    public void DeleteDocuments(string[] ids)
    {
        Task.Run(async () => await DeleteDocumentsAsync(ids)).Wait();
    }

    /// <summary>
    /// Deletes documents by their IDs asynchronously.
    /// </summary>
    public async Task DeleteDocumentsAsync(string[] ids)
    {
        ThrowIfCollectionNotInitialized();

        List<PointId> pointIds = ids.Select(id => new PointId { Uuid = id }).ToList();
        await client.DeleteAsync(collectionName, pointIds);
    }

    /// <summary>
    /// Deletes all documents of the collection asynchronously. The collection itself and its configuration are kept.
    /// </summary>
    public async Task DeleteAllDocumentsAsync()
    {
        ThrowIfCollectionNotInitialized();

        // an empty filter matches every point
        await client.DeleteAsync(collectionName, new Filter());
    }

    /// <summary>
    /// Performs a similarity search using an embedding vector.
    /// </summary>
    public VectorDocument[] QueryByEmbedding(
        float[] embedding,
        TornadoWhereOperator? where = null,
        int topK = 5,
        bool includeScore = true)
    {
        return Task.Run(async () =>
            await QueryByEmbeddingAsync(embedding, where, topK, includeScore)).Result;
    }

    /// <summary>
    /// Performs a similarity search using an embedding vector asynchronously.
    /// </summary>
    public async Task<VectorDocument[]> QueryByEmbeddingAsync(
        float[] embedding,
        TornadoWhereOperator? where = null,
        int topK = 5,
        bool includeScore = true)
    {
        ThrowIfCollectionNotInitialized();

        Filter? filter = null;
        if (where != null)
        {
            filter = ConvertToQdrantFilter(where);
        }

        IReadOnlyList<ScoredPoint> searchResults = await client.SearchAsync(
            collectionName,
            embedding,
            filter: filter,
            limit: (ulong)topK,
            payloadSelector: true,
            vectorsSelector: true
        );

        return searchResults.Select(result => ConvertToVectorDocument(result, includeScore))
            .ToArray();
    }

    /// <summary>
    /// Performs a similarity search using an embedding vector asynchronously.
    /// </summary>
    public async Task<VectorDocument[]> GetDocumentWhere(
        TornadoWhereOperator? where = null,
        uint limit = 5)
    {
        ThrowIfCollectionNotInitialized();

        Filter? filter = null;
        if (where != null)
        {
            filter = ConvertToQdrantFilter(where);
        }

        ScrollResponse searchResults = await client.ScrollAsync(
            collectionName,
            filter: filter,
            limit: limit,
            payloadSelector: true,
            vectorsSelector: true
        );

        return searchResults.Result.Select(ConvertToVectorDocument).ToArray();
    }

    /// <summary>
    /// Converts a Qdrant ScoredPoint to a VectorDocument.
    /// </summary>
    private static VectorDocument ConvertToVectorDocument(ScoredPoint scoredPoint, bool includeScore = true)
    {
        string? id = scoredPoint.Id.Uuid;
        float[] embedding = scoredPoint.Vectors?.Vector?.Data?.ToArray() ?? [];
        string? content = scoredPoint.Payload.TryGetValue("content", out Value? value)
            ? value.StringValue
            : string.Empty;

        Dictionary<string, object> metadata = new Dictionary<string, object>();
        foreach (KeyValuePair<string, Value> kvp in scoredPoint.Payload)
        {
            if (kvp.Key != "content")
            {
                metadata[kvp.Key] = ConvertFromValue(kvp.Value);
            }
        }

        float? score = includeScore ? scoredPoint.Score : null;

        return new VectorDocument(id, content, metadata, embedding, score);
    }

    /// <summary>
    /// Converts a Qdrant RetrievedPoint to a VectorDocument.
    /// </summary>
    private static VectorDocument ConvertToVectorDocument(RetrievedPoint retrievedPoint)
    {
        string? id = retrievedPoint.Id.Uuid;
        float[] embedding = retrievedPoint.Vectors?.Vector?.Data?.ToArray() ?? [];
        string? content = retrievedPoint.Payload.TryGetValue("content", out Value? value)
            ? value.StringValue
            : string.Empty;

        Dictionary<string, object> metadata = new Dictionary<string, object>();
        foreach (KeyValuePair<string, Value> kvp in retrievedPoint.Payload)
        {
            if (kvp.Key != "content")
            {
                metadata[kvp.Key] = ConvertFromValue(kvp.Value);
            }
        }

        return new VectorDocument(id, content, metadata, embedding);
    }

    /// <summary>
    /// Converts a TornadoWhereOperator to a Qdrant Filter.
    /// </summary>
    private static Filter? ConvertToQdrantFilter(TornadoWhereOperator whereOperator)
    {
        Dictionary<string, object>? whereDict = whereOperator.ToWhere();
        if (whereDict == null || whereDict.Count == 0)
        {
            return null;
        }

        Filter filter = new Filter();
        List<Condition> conditions = [];

        foreach (KeyValuePair<string, object> kvp in whereDict)
        {
            if (kvp is { Key: "$and", Value: object[] andValues })
            {
                // Handle AND operator
                List<Condition> must = [];
                foreach (object item in andValues)
                {
                    if (item is Dictionary<string, object> dict)
                    {
                        must.AddRange(ConvertDictionaryToConditions(dict));
                    }
                }
                filter.Must.AddRange(must);
            }
            else if (kvp is { Key: "$or", Value: object[] orValues })
            {
                // Handle OR operator
                List<Condition> should = [];
                foreach (object item in orValues)
                {
                    if (item is Dictionary<string, object> dict)
                    {
                        should.AddRange(ConvertDictionaryToConditions(dict));
                    }
                }
                filter.Should.AddRange(should);
            }
            else
            {
                // Handle simple field condition
                conditions.AddRange(ConvertDictionaryToConditions(whereDict));
                break;
            }
        }

        if (conditions.Count > 0)
        {
            filter.Must.AddRange(conditions);
        }

        return filter;
    }

    /// <summary>
    /// Converts a dictionary to Qdrant conditions.
    /// </summary>
    private static List<Condition> ConvertDictionaryToConditions(Dictionary<string, object> dict)
    {
        List<Condition> conditions = [];

        foreach (KeyValuePair<string, object> kvp in dict)
        {
            if (kvp.Value is Dictionary<string, object> opDict)
            {
                foreach (KeyValuePair<string, object> op in opDict)
                {
                    Condition? condition = null;

                    switch (op.Key)
                    {
                        case "$eq":
                            condition = CreateMatchCondition(kvp.Key, op.Value);
                            break;
                        case "$ne":
                            condition = Negate(CreateMatchCondition(kvp.Key, op.Value));
                            break;
                        case "$gt":
                            condition = Range(kvp.Key, new QdrantRange
                            {
                                Gt = ConvertToDouble(op.Value)
                            });
                            break;
                        case "$gte":
                            condition = Range(kvp.Key, new QdrantRange
                            {
                                Gte = ConvertToDouble(op.Value)
                            });
                            break;
                        case "$lt":
                            condition = Range(kvp.Key, new QdrantRange
                            {
                                Lt = ConvertToDouble(op.Value)
                            });
                            break;
                        case "$lte":
                            condition = Range(kvp.Key, new QdrantRange
                            {
                                Lte = ConvertToDouble(op.Value)
                            });
                            break;
                        case "$in":
                            if (op.Value is object[] { Length: > 0 } values)
                            {
                                condition = values[0] switch
                                {
                                    // Try to match strings or integers
                                    string => Match(kvp.Key, values.Select(v => v.ToString() ?? string.Empty).ToList()),
                                    int or long => Match(kvp.Key, values.Select(Convert.ToInt64).ToList()),
                                    _ => condition
                                };
                            }
                            break;
                        case "$nin":
                            if (op.Value is object[] { Length: > 0 } excluded)
                            {
                                condition = excluded[0] switch
                                {
                                    string => Negate(Match(kvp.Key, excluded.Select(v => v.ToString() ?? string.Empty).ToList())),
                                    int or long => Negate(Match(kvp.Key, excluded.Select(Convert.ToInt64).ToList())),
                                    _ => condition
                                };
                            }
                            break;
                    }

                    if (condition != null)
                    {
                        conditions.Add(condition);
                    }
                }
            }
        }

        return conditions;
    }

    /// <summary>
    /// Wraps a condition so it must not hold. Nested, so it also works inside should (OR) clauses.
    /// </summary>
    private static Condition Negate(Condition condition)
    {
        Filter filter = new Filter();
        filter.MustNot.Add(condition);
        return Conditions.Filter(filter);
    }

    /// <summary>
    /// Creates a Match condition based on the value type.
    /// </summary>
    private static Condition CreateMatchCondition(string key, object value)
    {
        return value switch
        {
            string str => MatchKeyword(key, str),
            int i => Match(key, i),
            long l => Match(key, l),
            bool b => Match(key, b),
            _ => MatchKeyword(key, value.ToString() ?? string.Empty)
        };
    }

    /// <summary>
    /// Converts a C# object to a Qdrant Value.
    /// </summary>
    private static Value ConvertToValue(object obj)
    {
        Value value = new Value();

        switch (obj)
        {
            case null:
                value.NullValue = NullValue.NullValue;
                break;
            case string str:
                value.StringValue = str;
                break;
            case int i:
                value.IntegerValue = i;
                break;
            case long l:
                value.IntegerValue = l;
                break;
            case double d:
                value.DoubleValue = d;
                break;
            case float f:
                value.DoubleValue = f;
                break;
            case bool b:
                value.BoolValue = b;
                break;
            default:
                value.StringValue = obj.ToString() ?? string.Empty;
                break;
        }

        return value;
    }

    /// <summary>
    /// Converts a Qdrant Value to a C# object.
    /// </summary>
    private static object ConvertFromValue(Value value)
    {
        return value.KindCase switch
        {
            Value.KindOneofCase.StringValue => value.StringValue,
            Value.KindOneofCase.IntegerValue => value.IntegerValue,
            Value.KindOneofCase.DoubleValue => value.DoubleValue,
            Value.KindOneofCase.BoolValue => value.BoolValue,
            Value.KindOneofCase.NullValue => null!,
            _ => string.Empty
        };
    }

    /// <summary>
    /// Converts an object to double for range queries.
    /// </summary>
    private static double ConvertToDouble(object obj)
    {
        return obj switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            _ => Convert.ToDouble(obj)
        };
    }
}
