using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.VectorDatabases;

public class ParentChildDocumentRetriever : IDocumentRetriever
{
    private IVectorDatabase _vectorStore;
    private IDocumentStore _docStore;

    public ParentChildDocumentRetriever(IVectorDatabase vectorStore, IDocumentStore docStore)
    {
        _vectorStore = vectorStore;
        _docStore = docStore;

        if(_vectorStore.GetCollectionName() != _docStore.GetCollectionName())
        {
            throw new ArgumentException("Vector store and document store must be for the same collection.");
        }
    }

    public async Task CreateParentChildCollection(string Text, 
        int parentSize, int parentOverlap, int chunkSize, int overlapSize, 
        IDocumentEmbeddingProvider embeddingProvider, 
        Dictionary<string,object>? additionalStaticParentMetadata = null,
        Dictionary<string, object>? additionalStaticChildMetadata = null)
    {
        List<VectorDocument> chunkedDocuments = new List<VectorDocument>();
        List<Document> parentDocs = new List<Document>();
        List<string> parents = TextTransformers.RecursiveCharacterTextSplitter(Text, parentSize, parentOverlap);
        
        for (int i = 0; i < parents.Count; i++)
        {
            List<string> chunks = TextTransformers.RecursiveCharacterTextSplitter(parents[i], chunkSize, overlapSize);
            Dictionary<string, object>? parentMetadata = additionalStaticParentMetadata;

            Document parent = new Document(Guid.NewGuid().ToString(), parents[i], parentMetadata);

            parentDocs.Add(parent);
            
            for( int j = 0; j < chunks.Count; j++)
            {
                float[] embeddings = await embeddingProvider.Invoke(chunks[j]);

                Dictionary<string, object> chunkMetadata = additionalStaticChildMetadata is not null ? new Dictionary<string, object>(additionalStaticChildMetadata) : new Dictionary<string, object>();
                chunkMetadata.Add("timestamp", DateTime.UtcNow.ToString("o"));
                chunkMetadata.Add("parent_id", parent.Id);
                chunkMetadata.Add("chunk_index", j);

                chunkedDocuments.Add(new VectorDocument(Guid.NewGuid().ToString(), chunks[j], chunkMetadata, embeddings)); 
            }
        }

        _vectorStore.AddDocuments(chunkedDocuments.ToArray());

        foreach (var doc in parentDocs)
        {
            _docStore.SetDocument(doc);
        }
    }

    public IEnumerable<Document> Search(float[] queryEmbedding, TornadoWhereOperator? where = null, int topK = 5, bool includeSource = false)
    {
        List<VectorDocument> vectorDocuments = _vectorStore.QueryByEmbedding(queryEmbedding, where, topK).ToList();

        return _docStore.GetDocuments(vectorDocuments
            .Where(d => d.Metadata != null && d.Metadata.ContainsKey("parent_id") && d.Metadata["parent_id"] != null)
            .Select(d => d.Metadata!["parent_id"].ToString()!)
            .ToArray());
    }

    public async Task<IEnumerable<Document>> SearchAsync(float[] queryEmbedding, TornadoWhereOperator? where = null, int topK = 5, bool includeSource = false)
    {
        List<VectorDocument> vectorDocuments = (await _vectorStore.QueryByEmbeddingAsync(queryEmbedding, where, topK)).ToList();

        return _docStore.GetDocuments(vectorDocuments
            .Where(d => d.Metadata != null && d.Metadata.ContainsKey("parent_id") && d.Metadata["parent_id"] != null)
            .Select(d => d.Metadata!["parent_id"].ToString()!)
            .ToArray());
    }
}
