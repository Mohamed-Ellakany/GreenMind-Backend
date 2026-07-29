using Greenmind.Application.Contracts.Storage;
using Microsoft.Extensions.Options;

namespace Greenmind.Infrastructure.Storage;

public sealed class ServerPrivateFileStorage(
    IOptions<PrivateFileStorageOptions> options) : IPrivateFileStorage
{
    private readonly string _rootPath = ResolveRoot(options.Value.RootPath);

    public async Task<string> SaveAsync(
        string area,
        string extension,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var safeArea = NormalizeArea(area);
        var safeExtension = NormalizeExtension(extension);
        var now = DateTime.UtcNow;
        var storageKey = $"{safeArea}/{now:yyyy/MM}/{Guid.NewGuid():N}{safeExtension}";
        var fullPath = ResolveKey(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var destination = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await content.CopyToAsync(destination, cancellationToken);
        return storageKey;
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            ResolveKey(storageKey),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveKey(storageKey);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string ResolveKey(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) ||
            Path.IsPathRooted(storageKey) ||
            storageKey.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid private storage key.");
        }

        var relativePath = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        if (!fullPath.StartsWith(
                _rootPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Private storage key escaped its root.");
        }

        return fullPath;
    }

    private static string ResolveRoot(string configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
            throw new InvalidOperationException("Private storage root is not configured.");
        return Path.GetFullPath(configuredRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string NormalizeArea(string area)
    {
        if (string.IsNullOrWhiteSpace(area) ||
            area.Any(character => !char.IsLetterOrDigit(character) && character != '-'))
        {
            throw new InvalidOperationException("Invalid private storage area.");
        }

        return area.ToLowerInvariant();
    }

    private static string NormalizeExtension(string extension)
    {
        var normalized = extension.Trim().ToLowerInvariant();
        return normalized is ".jpg" or ".jpeg" or ".png" or ".pdf"
            ? normalized
            : throw new InvalidOperationException("Unsupported private file extension.");
    }
}
