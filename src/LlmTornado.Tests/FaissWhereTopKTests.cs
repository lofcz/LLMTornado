using LlmTornado.VectorDatabases.Faiss;

namespace LlmTornado.Tests;

/// <summary>
/// QueryAsync applies a where filter after the neighbor search. Asking the index
/// for only topK neighbors drops matches that are real but not in that window.
/// </summary>
[TestFixture]
public class FaissWhereTopKTests
{
    private string _dir = "";
    private FaissCollectionClient _collection = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "faiss-where-" + Guid.NewGuid().ToString("N"));
        FaissClient client = new FaissClient(new FaissConfigurationOptions(_dir));
        FaissCollection collection = client.CreateCollectionAsync("docs", 2).GetAwaiter().GetResult();
        _collection = new FaissCollectionClient(collection, client);
    }

    [TearDown]
    public void TearDown()
    {
        _collection.Dispose();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    [Test]
    public void WhereFilter_ReturnsMatchesThatSitOutsideTheGlobalTopK()
    {
        Add(
            ("drop-near", new[] { 0f, 0f }, "drop"),
            ("drop-mid", new[] { 0.2f, 0f }, "drop"),
            ("keep-close", new[] { 1f, 0f }, "keep"),
            ("keep-far", new[] { 3f, 0f }, "keep"));

        List<FaissEntry> hits = Query(topK: 2, category: "keep");

        Assert.That(hits.Select(hit => hit.Id), Is.EqualTo(new[] { "keep-close", "keep-far" }));
        Assert.That(hits[0].Distance, Is.LessThan(hits[1].Distance));
    }

    [Test]
    public void WhereFilter_FillsTopKWhenOnlyPartOfTheGlobalWindowMatches()
    {
        Add(
            ("keep-near", new[] { 0f, 0f }, "keep"),
            ("drop-mid", new[] { 0.2f, 0f }, "drop"),
            ("keep-next", new[] { 1f, 0f }, "keep"),
            ("drop-far", new[] { 4f, 0f }, "drop"));

        List<FaissEntry> hits = Query(topK: 2, category: "keep");

        Assert.That(hits.Select(hit => hit.Id), Is.EqualTo(new[] { "keep-near", "keep-next" }));
    }

    [Test]
    public void WhereFilter_StopsAtTopKNearestMatches()
    {
        Add(
            ("keep-a", new[] { 0f, 0f }, "keep"),
            ("drop", new[] { 0.1f, 0f }, "drop"),
            ("keep-b", new[] { 1f, 0f }, "keep"),
            ("keep-c", new[] { 2f, 0f }, "keep"));

        List<FaissEntry> hits = Query(topK: 2, category: "keep");

        Assert.That(hits.Select(hit => hit.Id), Is.EqualTo(new[] { "keep-a", "keep-b" }));
    }

    [Test]
    public void QueryWithoutWhere_StillReturnsTheGlobalTopK()
    {
        Add(
            ("near", new[] { 0f, 0f }, "drop"),
            ("mid", new[] { 0.2f, 0f }, "keep"),
            ("far", new[] { 3f, 0f }, "keep"));

        List<FaissEntry> hits = _collection.QueryAsync(new[] { 0f, 0f }, 2).GetAwaiter().GetResult();

        Assert.That(hits.Select(hit => hit.Id), Is.EqualTo(new[] { "near", "mid" }));
    }

    private void Add(params (string Id, float[] Vector, string Category)[] rows)
    {
        _collection.AddAsync(
            rows.Select(row => row.Id).ToList(),
            rows.Select(row => row.Vector).ToList(),
            rows.Select(row => new Dictionary<string, object> { ["cat"] = row.Category }).ToList(),
            rows.Select(row => row.Id).ToList()).GetAwaiter().GetResult();
    }

    private List<FaissEntry> Query(int topK, string category)
    {
        return _collection.QueryAsync(
            new[] { 0f, 0f },
            topK,
            new Dictionary<string, object> { ["cat"] = category }).GetAwaiter().GetResult();
    }
}
