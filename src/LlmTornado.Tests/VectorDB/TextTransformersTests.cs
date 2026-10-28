using LlmTornado.VectorDatabases;

namespace LlmTornado.Tests.VectorDB;

[TestFixture]
public class TextTransformersTests
{
    private static string Words(int length)
    {
        string[] words = ["alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf"];
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int i = 0; sb.Length < length; i++)
        {
            sb.Append(words[i % words.Length]).Append(' ');
        }

        return sb.ToString(0, length).TrimEnd();
    }

    [Test]
    public void RecursiveCharacterTextSplitter_SingleLongLine_IsSplitToChunkSize()
    {
        // A PDF page is joined with spaces, so it reaches the splitter as one line.
        string page = Words(2500);

        List<string> chunks = TextTransformers.RecursiveCharacterTextSplitter(page, 1000, 200);

        Assert.Multiple(() =>
        {
            Assert.That(chunks.Count, Is.GreaterThan(2));
            Assert.That(chunks.Max(c => c.Length), Is.LessThanOrEqualTo(1000 + 200));
        });
    }

    [Test]
    public void RecursiveCharacterTextSplitter_LongLine_SplitsOnWordBoundaries()
    {
        string page = Words(2500);

        List<string> chunks = TextTransformers.RecursiveCharacterTextSplitter(page, 1000, 0);

        string[] expected = page.Split(' ');
        string[] actual = string.Join(" ", chunks.Select(c => c.Trim())).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void RecursiveCharacterTextSplitter_TextEndingInOverlap_DoesNotEmitTheOverlapAlone()
    {
        List<string> chunks = TextTransformers.RecursiveCharacterTextSplitter("0123456789", 10, 4);

        Assert.That(chunks, Is.EqualTo(new List<string> { "0123456789\n" }));
    }

    [Test]
    public void RecursiveCharacterTextSplitter_ShortLines_AreChunkedAsBefore()
    {
        string text = string.Join("\n", Enumerable.Range(0, 4).Select(i => new string((char)('a' + i), 30)));

        List<string> chunks = TextTransformers.RecursiveCharacterTextSplitter(text, 64, 16);

        Assert.That(chunks, Is.EqualTo(new List<string>
        {
            new string('a', 30) + "\n" + new string('b', 30) + "\n" + new string('c', 30) + "\n",
            new string('c', 15) + "\n" + new string('d', 30) + "\n"
        }));
    }

    [Test]
    public void RecursiveCharacterTextSplitter_LongLineEndingRightAfterAFullChunk_DoesNotThrow()
    {
        List<string> chunks = TextTransformers.RecursiveCharacterTextSplitter("aaaaaaaaa bbbb c", 10, 4);

        Assert.That(string.Join(" ", chunks).Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries), Does.Contain("c"));
    }

    [Test]
    public void RecursiveCharacterTextSplitter_RandomLongLines_NeverThrowAndKeepEveryWord()
    {
        Random random = new Random(42);

        for (int run = 0; run < 1000; run++)
        {
            int chunkSize = random.Next(5, 41);
            int overlap = random.Next(0, chunkSize);
            string text = string.Join("\n", Enumerable.Range(0, random.Next(1, 6)).Select(_ =>
                string.Join(" ", Enumerable.Range(0, random.Next(1, 60)).Select(__ => new string('x', random.Next(1, 8))))));

            List<string> chunks = null!;
            Assert.DoesNotThrow(() => chunks = TextTransformers.RecursiveCharacterTextSplitter(text, chunkSize, overlap), $"chunk_size={chunkSize}, overlap={overlap}");

            // A single line is cut to chunk_size (unless the overlap leaves room for a single character only)
            if (!text.Contains('\n') && text.Length > chunkSize && overlap <= chunkSize - 2)
            {
                Assert.That(chunks.Max(c => c.Length), Is.LessThanOrEqualTo(chunkSize), $"chunk_size={chunkSize}, overlap={overlap}");
            }

            int expectedChars = text.Count(c => c == 'x');
            Assert.That(chunks.Sum(c => c.Count(ch => ch == 'x')), Is.GreaterThanOrEqualTo(expectedChars), $"chunk_size={chunkSize}, overlap={overlap}");
        }
    }
}
