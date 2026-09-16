var samplesDir = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples"));

Directory.CreateDirectory(samplesDir);

const long targetBytes = (long)(2.8 * 1024 * 1024);
var pages = new List<SamplePage>();
var pageNumber = 1;
while (EstimateTextDocumentBytes(pages) < targetBytes)
{
    pages.Add(LargePage(pageNumber));
    pageNumber++;
}

var largePath = Path.Combine(samplesDir, "large-over-2mb.pdf");
File.WriteAllBytes(largePath, SamplePdf.CreateTextDocument("ChopDoc large sample", pages));

Console.WriteLine($"Wrote samples to {samplesDir}");
foreach (var file in Directory.GetFiles(samplesDir, "*.pdf"))
    Console.WriteLine($" - {Path.GetFileName(file)} ({new FileInfo(file).Length} bytes)");

static SamplePage LargePage(int number)
{
    var lines = new List<string>
    {
        $"This page is part of a text PDF larger than 2 MB. At the default 2 MB limit, HTML export should split into ordered parts. Page {number}."
    };
    for (var i = 1; i <= 28; i++)
    {
        lines.Add(
            $"Line {number}.{i}: ChopDoc converts the text layer to HTML, then packs pages into part N of M when the delivered file exceeds the size limit. This line exists so the sample file itself is larger than 2 MB.");
    }

    return new SamplePage($"Page {number}", lines);
}

static long EstimateTextDocumentBytes(IReadOnlyList<SamplePage> pages) =>
    800 + pages.Sum(page => 400 + page.Heading.Length + page.Lines.Sum(line => line.Length + 24));

internal sealed record SamplePage(string Heading, IReadOnlyList<string> Lines);

internal static class SamplePdf
{
    public static byte[] CreateTextDocument(string title, IReadOnlyList<SamplePage> pages)
    {
        var objects = new Dictionary<int, byte[]>();
        var pageNumbers = new List<int>();
        var next = 3;

        foreach (var page in pages)
        {
            var contentNumber = next++;
            var pageNumber = next++;
            pageNumbers.Add(pageNumber);
            objects[contentNumber] = StreamObject(BuildPageStream(title, page));
        }

        var fontNumber = next;
        objects[1] = Ascii("<< /Type /Catalog /Pages 2 0 R >>");
        objects[2] = Ascii($"<< /Type /Pages /Kids [ {string.Join(" ", pageNumbers.Select(n => $"{n} 0 R"))} ] /Count {pageNumbers.Count} >>");
        objects[fontNumber] = Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        for (var i = 0; i < pages.Count; i++)
        {
            var contentNumber = 3 + (i * 2);
            var pageNumber = contentNumber + 1;
            objects[pageNumber] = Ascii(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents {contentNumber} 0 R /Resources << /Font << /F1 {fontNumber} 0 R >> >> >>");
        }

        return Assemble(objects);
    }

    public static byte[] CreateImageOnlyPage(byte[] jpeg, int width, int height)
    {
        var objects = new Dictionary<int, byte[]>
        {
            [1] = Ascii("<< /Type /Catalog /Pages 2 0 R >>"),
            [2] = Ascii("<< /Type /Pages /Kids [ 3 0 R ] /Count 1 >>"),
            [3] = Ascii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /XObject << /Im1 5 0 R >> >> >>"),
            [4] = StreamObject("q\n540 0 0 696 36 48 cm\n/Im1 Do\nQ"),
            [5] = JpegObject(jpeg, width, height)
        };

        return Assemble(objects);
    }

    private static string BuildPageStream(string title, SamplePage page)
    {
        var commands = new List<string>
        {
            "BT",
            "/F1 11 Tf",
            "72 760 Td",
            $"({Escape(title)}) Tj",
            "/F1 18 Tf",
            "0 -36 Td",
            $"({Escape(page.Heading)}) Tj",
            "/F1 11 Tf",
            "0 -28 Td"
        };

        var firstBodyLine = true;
        foreach (var raw in page.Lines)
        {
            foreach (var line in Wrap(raw, 88))
            {
                if (!firstBodyLine)
                    commands.Add("0 -16 Td");
                firstBodyLine = false;
                commands.Add(line.Length == 0 ? "() Tj" : $"({Escape(line)}) Tj");
            }
        }

        commands.Add("ET");
        return string.Join("\n", commands);
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield return string.Empty;
            yield break;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = "";
        foreach (var word in words)
        {
            var next = current.Length == 0 ? word : current + " " + word;
            if (next.Length > width && current.Length > 0)
            {
                yield return current;
                current = word;
            }
            else
            {
                current = next;
            }
        }

        if (current.Length > 0)
            yield return current;
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static byte[] Ascii(string value) => System.Text.Encoding.ASCII.GetBytes(value);

    private static byte[] StreamObject(string content)
    {
        var body = System.Text.Encoding.ASCII.GetBytes(content);
        var header = System.Text.Encoding.ASCII.GetBytes($"<< /Length {body.Length} >>\nstream\n");
        var footer = "\nendstream"u8.ToArray();
        return Combine(header, body, footer);
    }

    private static byte[] JpegObject(byte[] jpeg, int width, int height)
    {
        var header = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
        var footer = "\nendstream"u8.ToArray();
        return Combine(header, jpeg, footer);
    }

    private static byte[] Assemble(Dictionary<int, byte[]> objectBodies)
    {
        var chunks = new List<byte[]> { "%PDF-1.4\n"u8.ToArray() };
        var offsets = new Dictionary<int, int>();
        var cursor = chunks[0].Length;

        foreach (var number in objectBodies.Keys.OrderBy(k => k))
        {
            offsets[number] = cursor;
            var chunk = Combine(
                System.Text.Encoding.ASCII.GetBytes($"{number} 0 obj\n"),
                objectBodies[number],
                "\nendobj\n"u8.ToArray());
            chunks.Add(chunk);
            cursor += chunk.Length;
        }

        var max = objectBodies.Keys.Max();
        var xref = new System.Text.StringBuilder();
        xref.Append("xref\n0 ").Append(max + 1).Append('\n');
        xref.Append("0000000000 65535 f \n");
        for (var i = 1; i <= max; i++)
            xref.Append(offsets[i].ToString("D10")).Append(" 00000 n \n");
        xref.Append("trailer<< /Size ").Append(max + 1).Append(" /Root 1 0 R >>\n");
        xref.Append("startxref\n").Append(cursor).Append("\n%%EOF");
        chunks.Add(System.Text.Encoding.ASCII.GetBytes(xref.ToString()));
        return Combine(chunks.ToArray());
    }

    private static byte[] Combine(params byte[][] parts)
    {
        var result = new byte[parts.Sum(p => p.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }
}
