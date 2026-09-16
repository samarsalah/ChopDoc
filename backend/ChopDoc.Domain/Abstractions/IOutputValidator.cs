using ChopDoc.Domain.Models;

namespace ChopDoc.Domain.Abstractions;

public interface IOutputValidator
{
    /// <summary>
    /// Structure and completeness, checked on the HTML intermediate because that is where the
    /// section markers live: part sequence, no duplicated sections, and every source section
    /// from <paramref name="expectedMarkers"/> present somewhere in the output.
    /// </summary>
    ValidationResult ValidateStructure(
        IReadOnlyList<SplitPartContent> parts,
        IReadOnlyCollection<string> expectedMarkers);

    /// <summary>
    /// Size and sequence of the artifacts actually handed off. Runs after export because the
    /// limit applies to the delivered format, not to the intermediate it was derived from.
    /// </summary>
    ValidationResult ValidateExportedParts(
        IReadOnlyList<ExportedPart> parts,
        long sizeLimitBytes);
}
