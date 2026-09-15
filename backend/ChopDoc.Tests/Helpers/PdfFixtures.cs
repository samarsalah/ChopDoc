using System.Text;

namespace ChopDoc.Tests.Helpers;

/// <summary>
/// Builds minimal PDFs without extra libraries so conversion tests stay deterministic.
/// </summary>
public static class PdfFixtures
{
    public static byte[] CreateTextPdf(params string[] pageTexts)
    {
        if (pageTexts.Length == 0)
            pageTexts = new[] { "ChopDoc sample text" };

        var objects = new List<string>();
        // Object numbers: 1=Catalog, 2=Pages, 3..=pages/content/font shared

        var pageObjectNumbers = new List<int>();
        var nextObj = 3;

        var contentObjectNumbers = new List<int>();
        var pageObjs = new List<(int pageObj, int contentObj, string text)>();

        foreach (var text in pageTexts)
        {
            var contentObj = nextObj++;
            var pageObj = nextObj++;
            contentObjectNumbers.Add(contentObj);
            pageObjectNumbers.Add(pageObj);
            pageObjs.Add((pageObj, contentObj, text));
        }

        var fontObj = nextObj++;

        // We'll assemble objects in order 1..n
        var catalogObj = 1;
        var pagesObj = 2;

        var kids = string.Join(" ", pageObjectNumbers.Select(n => $"{n} 0 R"));
        var objectBodies = new Dictionary<int, string>
        {
            [catalogObj] = "<< /Type /Catalog /Pages 2 0 R >>",
            [pagesObj] = $"<< /Type /Pages /Kids [ {kids} ] /Count {pageObjectNumbers.Count} >>",
            [fontObj] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        foreach (var (pageObj, contentObj, text) in pageObjs)
        {
            var escaped = EscapePdfString(text);
            var stream = $"BT /F1 18 Tf 72 720 Td ({escaped}) Tj ET";
            var streamBytes = Encoding.ASCII.GetBytes(stream);

            objectBodies[contentObj] =
                $"<< /Length {streamBytes.Length} >>\nstream\n{stream}\nendstream";

            objectBodies[pageObj] =
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] " +
                $"/Contents {contentObj} 0 R /Resources << /Font << /F1 {fontObj} 0 R >> >> >>";
        }

        return BuildPdf(objectBodies);
    }

    /// <summary>Valid PDF page with an empty content stream (no extractable text).</summary>
    public static byte[] CreateNoTextPdf()
    {
        var objectBodies = new Dictionary<int, string>
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [ 3 0 R ] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            [4] = "<< /Length 0 >>\nstream\nendstream"
        };

        return BuildPdf(objectBodies);
    }

    public static byte[] CreateCorruptedPdf() =>
        Encoding.ASCII.GetBytes("%PDF-1.4\nthis is not a valid pdf structure");

    private static string EscapePdfString(string value) =>
        value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static byte[] BuildPdf(Dictionary<int, string> objectBodies)
    {
        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");

        var offsets = new Dictionary<int, int>();
        foreach (var objNum in objectBodies.Keys.OrderBy(k => k))
        {
            offsets[objNum] = Encoding.ASCII.GetByteCount(sb.ToString());
            sb.Append(objNum).Append(" 0 obj\n");
            sb.Append(objectBodies[objNum]).Append('\n');
            sb.Append("endobj\n");
        }

        var xrefPos = Encoding.ASCII.GetByteCount(sb.ToString());
        var maxObj = objectBodies.Keys.Max();
        sb.Append("xref\n");
        sb.Append("0 ").Append(maxObj + 1).Append('\n');
        sb.Append("0000000000 65535 f \n");
        for (var i = 1; i <= maxObj; i++)
        {
            sb.Append(offsets[i].ToString("D10")).Append(" 00000 n \n");
        }

        sb.Append("trailer<< /Size ").Append(maxObj + 1).Append(" /Root 1 0 R >>\n");
        sb.Append("startxref\n").Append(xrefPos).Append('\n');
        sb.Append("%%EOF");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
