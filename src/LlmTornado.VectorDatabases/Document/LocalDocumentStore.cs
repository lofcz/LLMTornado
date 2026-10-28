using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LlmTornado.VectorDatabases;

public class LocalDocumentStore : IDocumentStore
{
    public string StorePath;

    public string CollectionName = "default_collection";
    private string _collectionFilePath => System.IO.Path.Combine(StorePath, CollectionName);

    public LocalDocumentStore(string storePath, string collectionName)
    {
        StorePath = storePath;
        SetCollection(collectionName);
    }

    public void SetCollection(string collectionName)
    {
        CollectionName = collectionName;
        if (!Directory.Exists(_collectionFilePath))
        {
            Directory.CreateDirectory(_collectionFilePath);
        }
    }

    public void DeleteDocument(string id)
    {
        var filePath = Path.Combine(_collectionFilePath, $"{id}.json");
        if (!File.Exists(filePath))
        {
            throw new KeyNotFoundException($"Document with id {id} not found.");
        }

        File.Delete(filePath);
    }

    public IEnumerable<Document> GetAllDocuments()
    {
        List<Document> documents = new List<Document>();
        if (Directory.Exists(_collectionFilePath))
        {
            var files = Directory.GetFiles(_collectionFilePath, "*.json");
            foreach (var file in files)
            {
                var json = File.ReadAllText(file);
                var doc = JsonSerializer.Deserialize<Document>(json);
                documents.Add(doc);
            }
        }
        return documents;
    }

    public IEnumerable<Document> GetDocuments(string[] id)
    {
        List<Document> documents = new List<Document>();
        foreach (var docId in id)
        {
            documents.Add(GetDocument(docId));
        }
        return documents;
    }

    public Document GetDocument(string id)
    {
        var filePath = Path.Combine(_collectionFilePath, $"{id}.json");
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Document>(json);
        }
        throw new KeyNotFoundException($"Document with id {id} not found.");
    }

    public void SetDocument(Document document)
    {
        string json = JsonSerializer.Serialize(document);
        File.WriteAllText(Path.Combine(_collectionFilePath, $"{document.Id}.json"), json);
    }

    public void UpdateDocument(Document document)
    {
        SetDocument(document);
    }


    public Task SetDocumentAsync(Document document)
    {
        SetDocument(document); return Task.CompletedTask;
    }

    public Task<IEnumerable<Document>> GetDocumentsAsync(string[] id)
    {
        return Task.FromResult(GetDocuments(id));
    }

    public Task UpdateDocumentAsync(Document document)
    {
        return Task.Run(() => UpdateDocument(document));
    }

    public Task DeleteDocumentAsync(string id)
    {
        return Task.Run(() => DeleteDocument(id));
    }

    public Task<IEnumerable<Document>> GetAllDocumentsAsync()
    {
        return Task.FromResult(GetAllDocuments());
    }

    public string GetCollectionName() => CollectionName;
}
