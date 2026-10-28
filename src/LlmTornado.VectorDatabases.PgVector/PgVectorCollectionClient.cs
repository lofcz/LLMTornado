using Npgsql;
using System.Text.Json;
using System.Text;

namespace LlmTornado.VectorDatabases.PgVector;

public class PgVectorCollectionClient
{
    private readonly PgVectorCollection _collection;
    private readonly PgVectorClient _client;

    public PgVectorCollectionClient(PgVectorCollection collection, PgVectorClient client)
    {
        _collection = collection;
        _client = client;
    }

    public PgVectorCollection Collection => _collection;

    private string GetTableName() => $"{_collection.Name}_vectors";

    public async Task<PgVectorEntry?> GetAsync(string id)
    {
        List<PgVectorEntry> entries = await GetAsync([id]);
        return entries.FirstOrDefault();
    }

    public async Task<List<PgVectorEntry>> GetAsync(string[] ids)
    {
        await using NpgsqlConnection connection = _client.CreateConnection();
        await connection.OpenAsync();

        string schema = _client.GetSchema();
        string tableName = GetTableName();
        string query = $@"
            SELECT id, document, metadata
            FROM {schema}.{tableName} 
            WHERE id = ANY(@ids)";

        await using NpgsqlCommand cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("ids", ids);

        List<PgVectorEntry> entries = [];
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string id = reader.GetString(0);
            string? document = reader.IsDBNull(1) ? null : reader.GetString(1);
            string? metadataJson = reader.IsDBNull(2) ? null : reader.GetString(2);
            Dictionary<string, object>? metadata = string.IsNullOrEmpty(metadataJson) 
                ? null 
                : JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson!);
            
            //float[]? embedding = null;
            //if (!reader.IsDBNull(3))
            //{
            //    var embeddingStr = reader.GetString(3);
            //    embedding = ParseVector(embeddingStr);
            //}

            entries.Add(new PgVectorEntry(id, document, metadata));
        }

        return entries;
    }

    string MetricOperator()
    {
        return _collection.Metric switch
        {
            SimilarityMetric.Cosine => "<=>",
            SimilarityMetric.Euclidean => "<->",
            SimilarityMetric.DotProduct => "<=>", // they return negative inner product, so use cosine
            SimilarityMetric.Manhattan => "<+>",
            _ => throw new ArgumentException()
        };
    }
    
    private string BuildQuerySql(string schema, string tableName, Dictionary<string, object>? whereMetadata)
    {
        StringBuilder queryBuilder = new StringBuilder();
        queryBuilder.AppendLine($"""
                             SELECT id, document, metadata, embedding, 
                                    (embedding {MetricOperator()} @embedding::vector) as distance 
                             FROM {schema}.{tableName}
                             """);

        if (whereMetadata is { Count: > 0 })
        {
            queryBuilder.AppendLine($" WHERE {BuildMetadataFilter(whereMetadata)}");
        }

        // the operators return a distance, so the closest rows come first when ordering ascending
        queryBuilder.AppendLine($"""
                                 ORDER BY distance asc
                                 LIMIT @limit
                                 """);

        return queryBuilder.ToString();
    }

    public async Task<List<PgVectorEntry>> QueryAsync(float[] queryEmbedding, int topK = 10, Dictionary<string, object>? whereMetadata = null)
    {
        await using NpgsqlConnection connection = _client.CreateConnection();
        await connection.OpenAsync();

        string sql = BuildQuerySql(_client.GetSchema(), GetTableName(), whereMetadata);
        await using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("embedding", $"[{string.Join(",", queryEmbedding)}]");
        cmd.Parameters.AddWithValue("limit", topK);

        List<PgVectorEntry> entries = [];
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string id = reader.GetString(0);
            string? document = reader.IsDBNull(1) ? null : reader.GetString(1);
            string? metadataJson = reader.IsDBNull(2) ? null : reader.GetString(2);
            Dictionary<string, object>? metadata = string.IsNullOrEmpty(metadataJson) 
                ? null 
                : JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson!);

            float[]? embedding = null;
            //if (!reader.IsDBNull(3))
            //{
            //    var embeddingStr = reader.GetString(3);
            //    embedding = ParseVector(embeddingStr);
            //}

            float distance = reader.GetFloat(4);

            entries.Add(new PgVectorEntry(id, document, metadata, embedding, distance));
        }

        return entries;
    }

    public async Task AddAsync(List<string> ids, List<float[]>? embeddings = null, List<Dictionary<string, object>>? metadatas = null, List<string>? documents = null)
    {
        if (ids.Count == 0) return;

        await using NpgsqlConnection connection = _client.CreateConnection();
        await connection.OpenAsync();

        string schema = _client.GetSchema();
        string tableName = GetTableName();
        
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        
        try
        {
            for (int i = 0; i < ids.Count; i++)
            {
                string query = $@"
                    INSERT INTO {schema}.{tableName} (id, document, metadata, embedding)
                    VALUES (@id, @document, @metadata::jsonb, @embedding::vector)";

                await using NpgsqlCommand cmd = new NpgsqlCommand(query, connection, transaction);
                cmd.Parameters.AddWithValue("id", ids[i]);
                cmd.Parameters.AddWithValue("document", documents != null && i < documents.Count ? (object)documents[i] : DBNull.Value);
                
                string metadataJson = metadatas != null && i < metadatas.Count 
                    ? JsonSerializer.Serialize(metadatas[i]) 
                    : "{}";
                cmd.Parameters.AddWithValue("metadata", metadataJson);
                
                string? embeddingStr = embeddings != null && i < embeddings.Count 
                    ? $"[{string.Join(",", embeddings[i])}]" 
                    : null;
                cmd.Parameters.AddWithValue("embedding", embeddingStr != null ? (object)embeddingStr : DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateAsync(List<string> ids, List<float[]>? embeddings = null, List<Dictionary<string, object>>? metadatas = null, List<string>? documents = null)
    {
        if (ids.Count == 0) return;

        await using NpgsqlConnection connection = _client.CreateConnection();
        await connection.OpenAsync();

        string schema = _client.GetSchema();
        string tableName = GetTableName();
        
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        
        try
        {
            for (int i = 0; i < ids.Count; i++)
            {
                List<string> setParts = [];
                NpgsqlCommand cmd = new NpgsqlCommand { Connection = connection, Transaction = transaction };

                if (documents != null && i < documents.Count)
                {
                    setParts.Add("document = @document");
                    cmd.Parameters.AddWithValue("document", documents[i]);
                }

                if (metadatas != null && i < metadatas.Count)
                {
                    setParts.Add("metadata = @metadata::jsonb");
                    cmd.Parameters.AddWithValue("metadata", JsonSerializer.Serialize(metadatas[i]));
                }

                if (embeddings != null && i < embeddings.Count)
                {
                    setParts.Add("embedding = @embedding::vector");
                    cmd.Parameters.AddWithValue("embedding", $"[{string.Join(",", embeddings[i])}]");
                }

                if (setParts.Count > 0)
                {
                    string query = $"""
                                    UPDATE {schema}.{tableName} 
                                    SET {string.Join(", ", setParts)}
                                    WHERE id = @id
                                    """;

                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue("id", ids[i]);

                    await cmd.ExecuteNonQueryAsync();
                }

                cmd.Dispose();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpsertAsync(List<string> ids, List<float[]>? embeddings = null, List<Dictionary<string, object>>? metadatas = null, List<string>? documents = null)
    {
        if (ids.Count == 0) return;

        await using NpgsqlConnection connection = _client.CreateConnection();
        await connection.OpenAsync();

        string schema = _client.GetSchema();
        string tableName = GetTableName();
        
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        
        try
        {
            for (int i = 0; i < ids.Count; i++)
            {
                string query = $"""
                                INSERT INTO {schema}.{tableName} (id, document, metadata, embedding)
                                VALUES (@id, @document, @metadata::jsonb, @embedding::vector)
                                ON CONFLICT (id) DO UPDATE SET
                                    document = EXCLUDED.document,
                                    metadata = EXCLUDED.metadata,
                                    embedding = EXCLUDED.embedding
                                """;

                await using NpgsqlCommand cmd = new NpgsqlCommand(query, connection, transaction);
                cmd.Parameters.AddWithValue("id", ids[i]);
                cmd.Parameters.AddWithValue("document", documents != null && i < documents.Count ? (object)documents[i] : DBNull.Value);
                
                string metadataJson = metadatas != null && i < metadatas.Count 
                    ? JsonSerializer.Serialize(metadatas[i]) 
                    : "{}";
                cmd.Parameters.AddWithValue("metadata", metadataJson);
                
                string? embeddingStr = embeddings != null && i < embeddings.Count 
                    ? $"[{string.Join(",", embeddings[i])}]" 
                    : null;
                cmd.Parameters.AddWithValue("embedding", embeddingStr != null ? (object)embeddingStr : DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteAsync(List<string> ids)
    {
        if (ids.Count == 0) return;

        await using NpgsqlConnection connection = _client.CreateConnection();
        await connection.OpenAsync();

        string schema = _client.GetSchema();
        string tableName = GetTableName();
        string query = $"DELETE FROM {schema}.{tableName} WHERE id = ANY(@ids)";

        await using NpgsqlCommand cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("ids", ids.ToArray());

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAllAsync()
    {
        await using NpgsqlConnection connection = _client.CreateConnection();
        await connection.OpenAsync();

        string schema = _client.GetSchema();
        string tableName = GetTableName();
        string query = $"DELETE FROM {schema}.{tableName}";

        await using NpgsqlCommand cmd = new NpgsqlCommand(query, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> CountAsync()
    {
        await using NpgsqlConnection connection = _client.CreateConnection();
        await connection.OpenAsync();

        string schema = _client.GetSchema();
        string tableName = GetTableName();
        string query = $"SELECT COUNT(*) FROM {schema}.{tableName}";

        await using NpgsqlCommand cmd = new NpgsqlCommand(query, connection);
        object? result = await cmd.ExecuteScalarAsync();
        
        return Convert.ToInt32(result);
    }

    private static string BuildMetadataFilter(Dictionary<string, object> whereMetadata)
    {
        List<string> conditions = [];
        
        foreach (KeyValuePair<string, object> kvp in whereMetadata)
        {
            string key = kvp.Key;
            object value = kvp.Value;

            if (key is "$and" or "$or")
            {
                if (value is IEnumerable<object> clauses)
                {
                    List<string> clauseConditions = clauses
                        .OfType<Dictionary<string, object>>()
                        .Select(BuildMetadataFilter)
                        .Where(clause => clause.Length > 0)
                        .Select(clause => $"({clause})")
                        .ToList();

                    if (clauseConditions.Count > 0)
                    {
                        conditions.Add($"({string.Join("$and" == key ? " AND " : " OR ", clauseConditions)})");
                    }
                }
            }
            else if (value is Dictionary<string, object> operatorDict)
            {
                foreach (KeyValuePair<string, object> op in operatorDict)
                {
                    string? condition = BuildCondition(key, op.Key, op.Value);
                    if (condition != null)
                    {
                        conditions.Add(condition);
                    }
                }
            }
            else
            {
                // Direct equality
                conditions.Add($"metadata->'{key}' = '\"{value}\"'");
            }
        }

        return string.Join(" AND ", conditions);
    }

    private static string? BuildCondition(string key, string op, object value)
    {
        return op switch
        {
            "$eq" => $"metadata->'{key}' = '{JsonSerializer.Serialize(value)}'",
            "$ne" => $"metadata->'{key}' != '{JsonSerializer.Serialize(value)}'",
            "$gt" => $"(metadata->'{key}')::numeric > {value}",
            "$gte" => $"(metadata->'{key}')::numeric >= {value}",
            "$lt" => $"(metadata->'{key}')::numeric < {value}",
            "$lte" => $"(metadata->'{key}')::numeric <= {value}",
            "$in" => value is Array arr 
                ? $"metadata->'{key}' IN ({string.Join(",", arr.Cast<object>().Select(v => $"'{JsonSerializer.Serialize(v)}'"))})" 
                : null,
            "$nin" => value is Array arr2 
                ? $"metadata->'{key}' NOT IN ({string.Join(",", arr2.Cast<object>().Select(v => $"'{JsonSerializer.Serialize(v)}'"))})" 
                : null,
            _ => null
        };
    }
}
