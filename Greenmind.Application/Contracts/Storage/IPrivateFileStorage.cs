namespace Greenmind.Application.Contracts.Storage;

public interface IPrivateFileStorage
{
    Task<string> SaveAsync(
        string area,
        string extension,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
