namespace ChopDoc.Domain.Enums;

/// <summary>
/// Conversion targets. HTML / Docx are implemented.
/// Other values are selectable so unsupported-format handling is visible end-to-end (OCP-ready).
/// </summary>
public enum OutputFormat
{
    Unspecified = 0,
    Html = 1,
    PlainText = 2,
    Markdown = 3,
    Docx = 4,
    Rtf = 5,
    Xml = 6,
    Json = 7,
    Csv = 8,
    Epub = 9,
    Odt = 10
}
