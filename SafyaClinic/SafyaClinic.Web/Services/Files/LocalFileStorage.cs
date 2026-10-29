namespace SafyaClinic.Web.Services.Files;

/// <summary>A file saved by <see cref="IFileStorage"/>; <c>Key</c> is what the database stores.</summary>
public sealed record StoredFile(string Key, string FileName, string ContentType, long Length);

/// <summary>A stored file located on disk and ready to stream.</summary>
public sealed record FileToServe(string FullPath, string ContentType, bool Inline, string DownloadName);

public interface IFileStorage
{
    /// <summary>Validates and saves an upload. Returns the stored file, or a message for the user.</summary>
    Task<(StoredFile? File, string? Error)> SaveAsync(IFormFile? file, string area);

    /// <summary>Finds a file by its key or by an old-style stored path. Null if unknown, missing or outside storage.</summary>
    FileToServe? Find(string? storedPath);

    /// <summary>Deletes the file if it exists.</summary>
    void Delete(string? storedPath);
}

/// <summary>
/// Attachments on local disk, outside wwwroot (slice S3).
/// Only PDF, JPEG and PNG are accepted, checked by extension and by the file's first bytes.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    public const string Analysis = "analysis";
    public const string Prescriptions = "prescriptions";
    private static readonly string[] Areas = { Analysis, Prescriptions };

    private static readonly Dictionary<string, (string ContentType, byte[] Signature)> Allowed =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ("application/pdf", "%PDF-"u8.ToArray()),
            [".png"] = ("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            [".jpg"] = ("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF }),
            [".jpeg"] = ("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF }),
        };

    private readonly string _root;
    private readonly long _maxBytes;

    public LocalFileStorage(string rootPath, long maxFileSizeBytes)
    {
        _root = Path.GetFullPath(rootPath);
        _maxBytes = maxFileSizeBytes;
    }

    public async Task<(StoredFile? File, string? Error)> SaveAsync(IFormFile? file, string area)
    {
        if (!Areas.Contains(area))
            throw new ArgumentException($"Unknown attachment area '{area}'.", nameof(area));

        if (file is null || file.Length == 0)
            return (null, "No file selected.");

        if (file.Length > _maxBytes)
            return (null, $"The file is too large. The limit is {_maxBytes / (1024 * 1024)} MB.");

        var extension = Path.GetExtension(file.FileName);
        if (!Allowed.TryGetValue(extension, out var rule))
            return (null, "Only PDF, JPG and PNG files can be attached.");

        var header = new byte[rule.Signature.Length];
        await using (var probe = file.OpenReadStream())
        {
            var read = await probe.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
            if (read < header.Length || !header.AsSpan().SequenceEqual(rule.Signature))
                return (null, "The file's content does not match its type. Only real PDF, JPG and PNG files can be attached.");
        }

        var fileName = $"{Guid.NewGuid()}{extension.ToLowerInvariant()}";
        var key = $"{area}/{fileName}";
        var fullPath = FullPathFor(key)!;

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using (var target = new FileStream(fullPath, FileMode.CreateNew))
        {
            await file.CopyToAsync(target);
        }

        return (new StoredFile(key, fileName, rule.ContentType, file.Length), null);
    }

    public FileToServe? Find(string? storedPath)
    {
        var fullPath = FullPathFor(ToKey(storedPath));
        if (fullPath is null || !File.Exists(fullPath)) return null;

        var name = Path.GetFileName(fullPath);
        return Allowed.TryGetValue(Path.GetExtension(name), out var rule)
            ? new FileToServe(fullPath, rule.ContentType, Inline: true, name)
            : new FileToServe(fullPath, "application/octet-stream", Inline: false, name);
    }

    public void Delete(string? storedPath)
    {
        var fullPath = FullPathFor(ToKey(storedPath));
        if (fullPath is not null && File.Exists(fullPath))
            File.Delete(fullPath);
    }

    /// <summary>
    /// "analysis/x.pdf", "wwwroot/uploads\analysis\x.pdf" and "D:\site\wwwroot\uploads\analysis\x.pdf"
    /// all become "analysis/x.pdf". Anything that is not area + file name gives null.
    /// </summary>
    public static string? ToKey(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return null;

        var parts = storedPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        var area = parts[^2].ToLowerInvariant();
        return Areas.Contains(area) ? $"{area}/{parts[^1]}" : null;
    }

    private string? FullPathFor(string? key)
    {
        if (key is null) return null;

        var parts = key.Split('/');
        var name = parts[1];
        if (name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;

        var fullPath = Path.GetFullPath(Path.Combine(_root, parts[0], name));
        return fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : null;
    }
}