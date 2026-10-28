using System.Reflection;
using System.Runtime.CompilerServices;
using LlmTornado.VectorDatabases;
using LlmTornado.VectorDatabases.Faiss;
using LlmTornado.VectorDatabases.PgVector;

namespace LlmTornado.Tests;

[TestFixture]
public class VectorLogicalFilterTests
{
    private static readonly Dictionary<string, object> Laptop = new()
    {
        ["category"] = "electronics",
        ["brand"] = "Apple"
    };

    [Test]
    public void Faiss_And_MatchesWhenBothClausesMatch()
    {
        Assert.That(FaissMatches(Laptop, TornadoWhereOperator.Equal("category", "electronics") & TornadoWhereOperator.Equal("brand", "Apple")), Is.True);
    }

    [Test]
    public void Faiss_And_DoesNotMatchWhenOneClauseFails()
    {
        Assert.That(FaissMatches(Laptop, TornadoWhereOperator.Equal("category", "electronics") & TornadoWhereOperator.Equal("brand", "Sony")), Is.False);
    }

    [Test]
    public void Faiss_Or_MatchesWhenOneClauseMatches()
    {
        Assert.That(FaissMatches(Laptop, TornadoWhereOperator.Equal("category", "computers") | TornadoWhereOperator.Equal("brand", "Apple")), Is.True);
    }

    [Test]
    public void Faiss_Or_DoesNotMatchWhenNoClauseMatches()
    {
        Assert.That(FaissMatches(Laptop, TornadoWhereOperator.Equal("category", "computers") | TornadoWhereOperator.Equal("brand", "Sony")), Is.False);
    }

    [Test]
    public void PgVector_And_JoinsTheClausesWithAnd()
    {
        string sql = PgVectorFilter(TornadoWhereOperator.Equal("category", "electronics") & TornadoWhereOperator.GreaterThan("price", 100));

        Assert.That(sql, Is.EqualTo("""((metadata->'category' = '"electronics"') AND ((metadata->'price')::numeric > 100))"""));
    }

    [Test]
    public void PgVector_Or_JoinsTheClausesWithOr()
    {
        string sql = PgVectorFilter(TornadoWhereOperator.Equal("category", "electronics") | TornadoWhereOperator.Equal("category", "computers"));

        Assert.That(sql, Is.EqualTo("""((metadata->'category' = '"electronics"') OR (metadata->'category' = '"computers"'))"""));
    }

    [Test]
    public void PgVector_NestedLogicalOperators_KeepTheirGrouping()
    {
        string sql = PgVectorFilter((TornadoWhereOperator.Equal("a", "x") | TornadoWhereOperator.Equal("b", "y")) & TornadoWhereOperator.Equal("c", "z"));

        Assert.That(sql, Does.StartWith("(((").And.Contain(" OR ").And.Contain(") AND ("));
    }

    [TestCase(SimilarityMetric.Cosine)]
    [TestCase(SimilarityMetric.Euclidean)]
    [TestCase(SimilarityMetric.DotProduct)]
    [TestCase(SimilarityMetric.Manhattan)]
    public void PgVector_Query_OrdersByDistanceAscendingSoTheClosestRowsAreReturned(SimilarityMetric metric)
    {
        PgVectorCollectionClient client = new PgVectorCollectionClient(new PgVectorCollection("docs", 3) { Metric = metric }, null!);
        MethodInfo method = typeof(PgVectorCollectionClient).GetMethod("BuildQuerySql", BindingFlags.NonPublic | BindingFlags.Instance)!;

        string sql = (string)method.Invoke(client, ["public", "docs_vectors", null])!;

        Assert.That(sql, Does.Contain("ORDER BY distance asc").And.Contain("LIMIT @limit"));
    }

    private static bool FaissMatches(Dictionary<string, object> metadata, TornadoWhereOperator where)
    {
        // MatchesFilter does not touch the index, so skip the native FAISS setup.
        FaissCollectionClient client = (FaissCollectionClient)RuntimeHelpers.GetUninitializedObject(typeof(FaissCollectionClient));
        MethodInfo method = typeof(FaissCollectionClient).GetMethod("MatchesFilter", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (bool)method.Invoke(client, [metadata, where.ToWhere()])!;
    }

    private static string PgVectorFilter(TornadoWhereOperator where)
    {
        MethodInfo method = typeof(PgVectorCollectionClient).GetMethod("BuildMetadataFilter", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, [where.ToWhere()])!;
    }
}
