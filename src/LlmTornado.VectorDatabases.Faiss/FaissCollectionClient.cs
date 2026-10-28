using System.Text.Json;

namespace LlmTornado.VectorDatabases.Faiss;

/// <summary>
/// Client for managing operations on a specific FAISS collection.
/// </summary>
public class FaissCollectionClient : IDisposable
{
    private readonly FaissCollection _collection;
    private readonly FaissClient _client;
    private FaissNet.Index? _index;
    private Dictionary<long, FaissEntry> _metadata;
    private long _nextId;
    private readonly object _lock = new object();
    private bool _isModified;

    public FaissCollectionClient(FaissCollection collection, FaissClient client)
    {
        _collection = collection;
        _client = client;
        _metadata = new Dictionary<long, FaissEntry>();
        _nextId = 0;
        _isModified = false;
        
        LoadIndex();
    }

    public FaissCollection Collection => _collection;

    private void LoadIndex()
    {
        lock (_lock)
        {
            string indexFile = _client.GetIndexFilePath(_collection.Name);
            string metadataFile = _client.GetMetadataFilePath(_collection.Name);

            if (File.Exists(indexFile) && File.Exists(metadataFile))
            {
                try
                {
                    // Load metadata
                    string json = File.ReadAllText(metadataFile);
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    
                    if (data != null)
                    {
                        if (data.TryGetValue("nextId", out var nextIdObj))
                        {
                            _nextId = Convert.ToInt64(nextIdObj);
                        }
                        
                        if (data.TryGetValue("entries", out var entriesObj))
                        {
                            var entriesJson = JsonSerializer.Serialize(entriesObj);
                            var entries = JsonSerializer.Deserialize<Dictionary<long, FaissEntry>>(entriesJson);
                            if (entries != null)
                            {
                                _metadata = entries;
                            }
                        }
                    }

                    // Load FAISS index
                    _index = FaissNet.Index.Load(indexFile);
                }
                catch
                {
                    // If loading fails, create new index
                    InitializeNewIndex();
                }
            }
            else
            {
                InitializeNewIndex();
            }
        }
    }

    private void InitializeNewIndex()
    {
        // Create a FAISS index with IDMap2 for managing custom IDs
        // Using Flat (exact search) with L2 distance
        _index = FaissNet.Index.Create(_collection.VectorDimension, "IDMap2,Flat", FaissNet.MetricType.METRIC_L2);
        _metadata = new Dictionary<long, FaissEntry>();
        _nextId = 0;
        _isModified = true;
    }

    private void SaveIndex()
    {
        lock (_lock)
        {
            if (!_isModified || _index == null)
            {
                return;
            }

            string indexFile = _client.GetIndexFilePath(_collection.Name);
            string metadataFile = _client.GetMetadataFilePath(_collection.Name);

            // Save FAISS index
            _index.Save(indexFile);

            // Save metadata
            var data = new Dictionary<string, object>
            {
                ["nextId"] = _nextId,
                ["entries"] = _metadata
            };
            
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metadataFile, json);

            _isModified = false;
        }
    }

    /// <summary>
    /// Gets entries by their string IDs.
    /// </summary>
    public Task<List<FaissEntry>> GetAsync(string[] ids)
    {
        lock (_lock)
        {
            var entries = new List<FaissEntry>();
            
            foreach (var id in ids)
            {
                var entry = _metadata.Values.FirstOrDefault(e => e.Id == id);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            
            return Task.FromResult(entries);
        }
    }

    /// <summary>
    /// Queries the index for the k nearest neighbors.
    /// </summary>
    public async Task<List<FaissEntry>> QueryAsync(float[] queryEmbedding, int topK = 10, Dictionary<string, object>? whereMetadata = null)
    {
        lock (_lock)
        {
            if (_index == null || _index.Count == 0)
            {
                return new List<FaissEntry>();
            }

            // Search in FAISS
            var (distances, labels) = _index.Search(new[] { queryEmbedding }, topK);
            
            var entries = new List<FaissEntry>();
            for (int i = 0; i < labels[0].Length; i++)
            {
                long faissId = labels[0][i];
                
                if (faissId >= 0 && _metadata.TryGetValue(faissId, out var entry))
                {
                    // Apply metadata filtering if specified
                    if (whereMetadata != null && !MatchesFilter(entry.Metadata, whereMetadata))
                    {
                        continue;
                    }
                    
                    var resultEntry = new FaissEntry(
                        entry.Id,
                        entry.Document,
                        entry.Metadata,
                        entry.Embedding,
                        distances[0][i]
                    );
                    entries.Add(resultEntry);
                }
            }
            
            return entries;
        }
    }

    /// <summary>
    /// Adds new entries to the index.
    /// </summary>
    public Task AddAsync(List<string> ids, List<float[]>? embeddings = null, List<Dictionary<string, object>>? metadatas = null, List<string>? documents = null)
    {
        lock (_lock)
        {
            if (ids.Count == 0 || _index == null)
            {
                return Task.CompletedTask;
            }

            var vectors = new List<float[]>();
            var faissIds = new List<long>();

            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var embedding = embeddings != null && i < embeddings.Count ? embeddings[i] : null;
                var metadata = metadatas != null && i < metadatas.Count ? metadatas[i] : null;
                var document = documents != null && i < documents.Count ? documents[i] : null;

                if (embedding == null)
                {
                    throw new ArgumentException($"Embedding is required for document '{id}'");
                }

                if (embedding.Length != _collection.VectorDimension)
                {
                    throw new ArgumentException($"Embedding dimension mismatch. Expected {_collection.VectorDimension}, got {embedding.Length}");
                }

                long faissId = _nextId++;
                var entry = new FaissEntry(id, document, metadata, embedding);
                _metadata[faissId] = entry;

                vectors.Add(embedding);
                faissIds.Add(faissId);
            }

            // Add to FAISS index
            _index.AddWithIds(vectors.ToArray(), faissIds.ToArray());
            _isModified = true;
            SaveIndex();

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Updates existing entries in the index.
    /// </summary>
    public Task UpdateAsync(List<string> ids, List<float[]>? embeddings = null, List<Dictionary<string, object>>? metadatas = null, List<string>? documents = null)
    {
        lock (_lock)
        {
            if (ids.Count == 0)
            {
                return Task.CompletedTask;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var faissId = _metadata.FirstOrDefault(kvp => kvp.Value.Id == id).Key;
                
                if (_metadata.TryGetValue(faissId, out var entry))
                {
                    if (documents != null && i < documents.Count)
                    {
                        entry.Document = documents[i];
                    }
                    
                    if (metadatas != null && i < metadatas.Count)
                    {
                        entry.Metadata = metadatas[i];
                    }
                    
                    // Note: FAISS doesn't support in-place updates of vectors
                    // For now, we only update metadata and document content
                    // To update embeddings, use Upsert which will delete and re-add
                }
            }

            _isModified = true;
            SaveIndex();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Upserts entries (insert or update).
    /// </summary>
    public Task UpsertAsync(List<string> ids, List<float[]>? embeddings = null, List<Dictionary<string, object>>? metadatas = null, List<string>? documents = null)
    {
        lock (_lock)
        {
            var toAdd = new List<string>();
            var toAddEmbeddings = new List<float[]>();
            var toAddMetadatas = new List<Dictionary<string, object>>();
            var toAddDocuments = new List<string>();

            var toUpdate = new List<string>();
            var toUpdateMetadatas = new List<Dictionary<string, object>>();
            var toUpdateDocuments = new List<string>();

            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var exists = _metadata.Values.Any(e => e.Id == id);

                if (exists)
                {
                    // If embedding is provided for existing entry, we need to delete and re-add
                    if (embeddings != null && i < embeddings.Count)
                    {
                        DeleteAsync(new List<string> { id }).Wait();
                        toAdd.Add(id);
                        toAddEmbeddings.Add(embeddings[i]);
                        toAddMetadatas.Add(metadatas != null && i < metadatas.Count ? metadatas[i] : new Dictionary<string, object>());
                        toAddDocuments.Add(documents != null && i < documents.Count ? documents[i] : "");
                    }
                    else
                    {
                        toUpdate.Add(id);
                        if (metadatas != null && i < metadatas.Count)
                        {
                            toUpdateMetadatas.Add(metadatas[i]);
                        }
                        if (documents != null && i < documents.Count)
                        {
                            toUpdateDocuments.Add(documents[i]);
                        }
                    }
                }
                else
                {
                    toAdd.Add(id);
                    toAddEmbeddings.Add(embeddings != null && i < embeddings.Count ? embeddings[i] : throw new ArgumentException($"Embedding required for new entry '{id}'"));
                    toAddMetadatas.Add(metadatas != null && i < metadatas.Count ? metadatas[i] : new Dictionary<string, object>());
                    toAddDocuments.Add(documents != null && i < documents.Count ? documents[i] : "");
                }
            }

            if (toAdd.Count > 0)
            {
                AddAsync(toAdd, toAddEmbeddings, toAddMetadatas, toAddDocuments).Wait();
            }

            if (toUpdate.Count > 0)
            {
                UpdateAsync(toUpdate, null, toUpdateMetadatas, toUpdateDocuments).Wait();
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Deletes entries from the index.
    /// </summary>
    public Task DeleteAsync(List<string> ids)
    {
        lock (_lock)
        {
            if (ids.Count == 0 || _index == null)
            {
                return Task.CompletedTask;
            }

            var idsToRemove = new List<long>();
            foreach (var id in ids)
            {
                var kvp = _metadata.FirstOrDefault(e => e.Value.Id == id);
                if (kvp.Value != null)
                {
                    idsToRemove.Add(kvp.Key);
                }
            }

            foreach (var faissId in idsToRemove)
            {
                _metadata.Remove(faissId);
            }

            if (idsToRemove.Count > 0)
            {
                try
                {
                    // Try to remove from FAISS index
                    _index.RemoveIds(idsToRemove.ToArray());
                }
                catch
                {
                    // If RemoveIds is not supported, rebuild the index
                    RebuildIndex();
                }
                _isModified = true;
                SaveIndex();
            }

            return Task.CompletedTask;
        }
    }

    private void RebuildIndex()
    {
        // Dispose old index
        _index?.Dispose();
        
        // Create new index
        _index = FaissNet.Index.Create(_collection.VectorDimension, "IDMap2,Flat", FaissNet.MetricType.METRIC_L2);

        // Re-add all remaining entries
        if (_metadata.Count > 0)
        {
            var vectors = new List<float[]>();
            var faissIds = new List<long>();

            foreach (var kvp in _metadata)
            {
                if (kvp.Value.Embedding != null)
                {
                    vectors.Add(kvp.Value.Embedding);
                    faissIds.Add(kvp.Key);
                }
            }

            if (vectors.Count > 0)
            {
                _index.AddWithIds(vectors.ToArray(), faissIds.ToArray());
            }
        }
    }

    /// <summary>
    /// Gets the count of entries in the index.
    /// </summary>
    public Task<int> CountAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_metadata.Count);
        }
    }

    private bool MatchesFilter(Dictionary<string, object>? metadata, Dictionary<string, object> filter)
    {
        if (metadata == null)
        {
            return false;
        }

        foreach (var filterKvp in filter)
        {
            var key = filterKvp.Key;
            var filterValue = filterKvp.Value;

            if (key is "$and" or "$or")
            {
                if (filterValue is not IEnumerable<object> clauses)
                {
                    return false;
                }

                var clauseMatches = clauses
                    .OfType<Dictionary<string, object>>()
                    .Select(clause => MatchesFilter(metadata, clause))
                    .ToList();

                if (key == "$and" ? !clauseMatches.All(matched => matched) : !clauseMatches.Any(matched => matched))
                {
                    return false;
                }

                continue;
            }

            if (!metadata.TryGetValue(key, out var metadataValue))
            {
                return false;
            }

            if (filterValue is Dictionary<string, object> operatorDict)
            {
                foreach (var op in operatorDict)
                {
                    if (!MatchesOperator(metadataValue, op.Key, op.Value))
                    {
                        return false;
                    }
                }
            }
            else
            {
                // Direct equality
                if (!metadataValue.Equals(filterValue))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool MatchesOperator(object metadataValue, string op, object filterValue)
    {
        try
        {
            switch (op)
            {
                case "$eq":
                    return metadataValue.Equals(filterValue);
                case "$ne":
                    return !metadataValue.Equals(filterValue);
                case "$gt":
                    return Convert.ToDouble(metadataValue) > Convert.ToDouble(filterValue);
                case "$gte":
                    return Convert.ToDouble(metadataValue) >= Convert.ToDouble(filterValue);
                case "$lt":
                    return Convert.ToDouble(metadataValue) < Convert.ToDouble(filterValue);
                case "$lte":
                    return Convert.ToDouble(metadataValue) <= Convert.ToDouble(filterValue);
                case "$in":
                    if (filterValue is Array arr)
                    {
                        return arr.Cast<object>().Contains(metadataValue);
                    }
                    return false;
                case "$nin":
                    if (filterValue is Array arr2)
                    {
                        return !arr2.Cast<object>().Contains(metadataValue);
                    }
                    return false;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        SaveIndex();
        _index?.Dispose();
    }
}
