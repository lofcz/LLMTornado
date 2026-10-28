using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace LlmTornado.VectorDatabases
{
    public class TextTransformers
    {
        /// <summary>
        /// Splits the input text into chunks of approximately chunkSize characters, preserving line boundaries,
        /// and keeping the last chunkOverlap characters as the start of the next chunk (overlap).
        /// </summary>
        /// <param name="texts">Full text to split (required, not null).</param>
        /// <param name="chunk_size">Target maximum size (in chars) for a chunk (must be > 0).</param>
        /// <param name="chunk_overlap">Number of tail characters from a completed chunk to prepend to the next (0 <= overlap < chunk_size).</param>
        public static List<string> RecursiveCharacterTextSplitter(string texts, int chunk_size=256, int chunk_overlap=64)
        {
            if (texts == null) throw new ArgumentNullException(nameof(texts));
            if (chunk_size <= 0) throw new ArgumentOutOfRangeException(nameof(chunk_size), "chunk_size must be > 0.");
            if (chunk_overlap < 0) throw new ArgumentOutOfRangeException(nameof(chunk_overlap), "chunk_overlap must be >= 0.");
            if (chunk_overlap >= chunk_size) throw new ArgumentException("chunk_overlap must be < chunk_size.", nameof(chunk_overlap));

            // Normalize line endings to avoid Windows \r issues splitting only on '\n'
            var normalized = texts.Replace("\r\n", "\n");
            var rawLines = normalized.Split('\n'); // StringSplitOptions.None to preserve empty lines

            var chunks = new List<string>();
            var sb = new StringBuilder();
            // Characters at the start of sb that are only overlap carried over from the previous chunk
            var carried = 0;

            void Emit()
            {
                chunks.Add(sb.ToString());

                if (chunk_overlap > 0)
                {
                    // Keep only the overlap tail for the next chunk
                    var current = sb;
                    var keepCount = Math.Min(chunk_overlap, current.Length);
                    var start = current.Length - keepCount;
                    var tail = current.ToString(start, keepCount);
                    sb.Clear();
                    sb.Append(tail);
                    carried = keepCount;
                }
                else
                {
                    sb.Clear();
                    carried = 0;
                }
            }

            void Append(string text, char separator)
            {
                sb.Append(text);
                sb.Append(separator);
                carried = 0;

                if (sb.Length >= chunk_size)
                {
                    Emit();
                }
            }

            foreach (var line in rawLines)
            {
                // A line longer than chunk_size (e.g. PDF page text, which is joined with spaces and has no newlines)
                // is cut at the last space that still fits into the current chunk
                var pos = 0;
                while (line.Length > chunk_size && line.Length - pos > chunk_size - sb.Length - 1)
                {
                    var room = chunk_size - sb.Length - 1;
                    if (room <= 0 && sb.Length > carried)
                    {
                        // The chunk is full: close it before adding more
                        Emit();
                        continue;
                    }

                    var width = Math.Max(room, 1);
                    var end = Math.Min(pos + width, line.Length - 1);
                    var space = line.LastIndexOf(' ', end, end - pos + 1);
                    if (space > pos)
                    {
                        Append(line.Substring(pos, space - pos), ' ');
                        pos = space + 1;
                    }
                    else if (sb.Length > carried)
                    {
                        // No space fits next to the text already in the chunk: close it instead of cutting a word
                        Emit();
                    }
                    else
                    {
                        Append(line.Substring(pos, width), ' ');
                        pos += width;
                    }
                }

                Append(line.Substring(pos), '\n');
            }

            //Add any remaining non-whitespace content (the overlap tail of the last chunk alone is not new content)
            if (sb.Length > carried && sb.ToString().Trim().Length > 0)
            {
                chunks.Add(sb.ToString());
            }

            return chunks;
        }
    }
}
