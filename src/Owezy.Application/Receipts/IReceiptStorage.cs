namespace Owezy.Application.Receipts;

/// <summary>
/// Receipt image storage abstraction. Infrastructure provides the local filesystem implementation.
/// The application never knows where images are physically stored.
/// </summary>
public interface IReceiptStorage
{
    /// <summary>
    /// Stores the image stream and returns a server-generated opaque storage key.
    /// The caller must NOT use the original client filename.
    /// </summary>
    Task<string> StoreAsync(Stream imageStream, string fileExtension, CancellationToken cancellationToken = default);
}
