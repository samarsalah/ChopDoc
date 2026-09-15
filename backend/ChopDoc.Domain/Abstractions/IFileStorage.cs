namespace ChopDoc.Domain.Abstractions;

public interface IFileStorage
{
    Task<string> SaveAsync(
        Stream content,
        string relativeFolder,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<string> SaveAsync(
        byte[] content,
        string relativeFolder,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string storedPath, CancellationToken cancellationToken = default);

    Task<byte[]> ReadAllBytesAsync(string storedPath, CancellationToken cancellationToken = default);
}
