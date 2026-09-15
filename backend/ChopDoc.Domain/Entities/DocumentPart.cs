namespace ChopDoc.Domain.Entities;

public class DocumentPart
{
    private DocumentPart()
    {
        // EF Core
    }

    public DocumentPart(
        Guid jobId,
        int partNumber,
        int totalParts,
        string storedPath,
        long sizeBytes,
        string fileName)
    {
        if (partNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(partNumber));

        if (totalParts < 1)
            throw new ArgumentOutOfRangeException(nameof(totalParts));

        if (partNumber > totalParts)
            throw new ArgumentException("Part number cannot exceed total parts.");

        if (sizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));

        if (string.IsNullOrWhiteSpace(storedPath))
            throw new ArgumentException("Stored path is required.", nameof(storedPath));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));

        Id = Guid.NewGuid();
        JobId = jobId;
        PartNumber = partNumber;
        TotalParts = totalParts;
        StoredPath = storedPath;
        SizeBytes = sizeBytes;
        FileName = fileName;
        SequenceLabel = $"part {partNumber} of {totalParts}";
    }

    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public int PartNumber { get; private set; }
    public int TotalParts { get; private set; }
    public string SequenceLabel { get; private set; } = string.Empty;
    public string StoredPath { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
}
