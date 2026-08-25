using Owezy.Application.Receipts;

namespace Owezy.Infrastructure.Storage;

/// <summary>
/// Stores receipt images on the local filesystem.
/// Storage directory is configurable; defaults to LOCALAPPDATA\Owezy\receipts.
/// Storage keys are server-generated GUIDs with extension — client filenames are NEVER used.
/// Prevents path traversal: only the safe generated key is used to form paths.
/// </summary>
public sealed class LocalFileReceiptStorage : IReceiptStorage
{
    private readonly string _storageRoot;

    public LocalFileReceiptStorage(string? storageRoot = null)
    {
        _storageRoot = storageRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Owezy", "receipts");
    }

    public async Task<string> StoreAsync(Stream imageStream, string fileExtension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        if (string.IsNullOrWhiteSpace(fileExtension))
            throw new ArgumentException("File extension cannot be empty.", nameof(fileExtension));

        // Ensure extension is safe: strip any path separators, allow only alphanumeric
        var safeExtension = SanitizeExtension(fileExtension);

        // Server-generated key — NEVER derived from client input
        var storageKey = $"{Guid.NewGuid():N}.{safeExtension}";

        // Prevent path traversal: build path from clean root + safe key only
        Directory.CreateDirectory(_storageRoot);
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, storageKey));

        // Verify the resolved path is still under the storage root
        if (!fullPath.StartsWith(Path.GetFullPath(_storageRoot), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path traversal detected.");

        if (imageStream.CanSeek)
            imageStream.Seek(0, SeekOrigin.Begin);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await imageStream.CopyToAsync(fileStream, cancellationToken);

        return storageKey;
    }

    private static string SanitizeExtension(string extension)
    {
        // Remove leading dot if present, strip any non-alphanumeric chars
        var ext = extension.TrimStart('.').ToLowerInvariant();
        ext = System.Text.RegularExpressions.Regex.Replace(ext, @"[^a-z0-9]", "");
        if (string.IsNullOrWhiteSpace(ext))
            throw new ArgumentException("Extension is invalid after sanitisation.");
        return ext;
    }
}
