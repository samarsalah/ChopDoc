using ChopDoc.Domain.Models;

namespace ChopDoc.Domain.Abstractions;

public interface IOutputValidator
{
    ValidationResult Validate(
        IReadOnlyList<SplitPartContent> parts,
        long sizeLimitBytes);
}
