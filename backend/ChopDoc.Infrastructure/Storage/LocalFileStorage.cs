using ChopDoc.Domain.Abstractions;
using ChopDoc.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace ChopDoc.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        _rootPath = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(
        Stream content,
        string relativeFolder,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var relativePath = BuildRelativePath(relativeFolder, fileName);
        var fullPath = GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, cancellationToken);
        return relativePath;
    }

    public async Task<string> SaveAsync(
        byte[] content,
        string relativeFolder,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new MemoryStream(content);
        return await SaveAsync(stream, relativeFolder, fileName, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = GetFullPath(storedPath);
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public async Task<byte[]> ReadAllBytesAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(storedPath);
        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    private string BuildRelativePath(string relativeFolder, string fileName)
    {
        var folder = relativeFolder.Replace('\\', '/').Trim('/');
        var name = Path.GetFileName(fileName);
        return string.IsNullOrEmpty(folder) ? name : $"{folder}/{name}";
    }

    private string GetFullPath(string storedPath)
    {
        var combined = Path.GetFullPath(Path.Combine(_rootPath, storedPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!combined.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid storage path.");

        return combined;
    }
}
