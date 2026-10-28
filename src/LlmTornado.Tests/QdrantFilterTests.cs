using System.Reflection;
using LlmTornado.VectorDatabases;
using LlmTornado.VectorDatabases.Qdrant;
using Qdrant.Client.Grpc;

namespace LlmTornado.Tests;

[TestFixture]
public class QdrantFilterTests
{
    [Test]
    public void NotEqual_BecomesAMustNotCondition()
    {
        Filter filter = Convert(TornadoWhereOperator.NotEqual("category", "spam"));

        Assert.That(filter.Must, Has.Count.EqualTo(1));
        Filter negated = filter.Must[0].Filter;
        Assert.That(negated.MustNot, Has.Count.EqualTo(1));
        Assert.That(negated.MustNot[0].Field.Key, Is.EqualTo("category"));
        Assert.That(negated.MustNot[0].Field.Match.Keyword, Is.EqualTo("spam"));
    }

    [Test]
    public void NotIn_BecomesAMustNotCondition()
    {
        Filter filter = Convert(TornadoWhereOperator.NotIn("category", "spam", "ads"));

        Assert.That(filter.Must, Has.Count.EqualTo(1));
        Filter negated = filter.Must[0].Filter;
        Assert.That(negated.MustNot, Has.Count.EqualTo(1));
        Assert.That(negated.MustNot[0].Field.Match.Keywords.Strings, Is.EqualTo(new[] { "spam", "ads" }));
    }

    [Test]
    public void NotEqual_CombinedWithAnotherClause_KeepsBoth()
    {
        Filter filter = Convert(TornadoWhereOperator.Equal("lang", "en") & TornadoWhereOperator.NotEqual("category", "spam"));

        Assert.That(filter.Must, Has.Count.EqualTo(2));
        Assert.That(filter.Must[0].Field.Key, Is.EqualTo("lang"));
        Assert.That(filter.Must[1].Filter.MustNot[0].Field.Key, Is.EqualTo("category"));
    }

    private static Filter Convert(TornadoWhereOperator where)
    {
        MethodInfo method = typeof(QdrantVectorDatabase).GetMethod("ConvertToQdrantFilter", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Filter)method.Invoke(null, [where])!;
    }
}
