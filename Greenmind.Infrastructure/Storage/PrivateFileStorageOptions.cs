namespace Greenmind.Infrastructure.Storage;

public sealed class PrivateFileStorageOptions
{
    public const string SectionName = "PrivateFileStorage";
    public string RootPath { get; set; } = string.Empty;
}
