using System.Text.RegularExpressions;
using EthioServe.Application.Abstractions;

namespace EthioServe.Infrastructure.Storage;

/// <summary>
/// Stores files in a private folder on this machine. Only keys the application generates are accepted
/// (32 hex characters and a known image extension), so a key can never point outside the folder.
/// Production must set Storage:RootPath; when it is missing a folder in the system temp directory is used.
/// </summary>
public sealed partial class LocalFileStorage : IFileStorage
{
    private const int BufferSize = 81_920;

    private readonly string _root;

    public LocalFileStorage(string? rootPath)
    {
        var root = string.IsNullOrWhiteSpace(rootPath)
            ? Path.Combine(Path.GetTempPath(), "ethioserve-uploads")
            : rootPath;

        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        Directory.CreateDirectory(_root);
    }

    [GeneratedRegex(@"^[0-9a-f]{32}\.(jpg|png|webp)\z", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    public async Task SaveAsync(string key, Stream content, CancellationToken ct = default)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // CreateNew: a key can never overwrite an existing file.
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var path = PathFor(key);

        try
        {
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);

            return Task.FromResult<Stream?>(stream);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        File.Delete(PathFor(key));

        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        if (!KeyPattern().IsMatch(key))
        {
            throw new ArgumentException("Invalid storage key.", nameof(key));
        }

        var path = Path.GetFullPath(Path.Combine(_root, key[..2], key));

        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid storage key.", nameof(key));
        }

        return path;
    }
}