using System.Security.Cryptography;
using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Ivan.Infrastructure.Services;

public class AttachmentStorage : IAttachmentStorage
{
    private readonly IIvanDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly string _storageRoot;

    public AttachmentStorage(
        IIvanDbContext db,
        ICurrentUserContext userContext,
        IConfiguration configuration)
    {
        _db = db;
        _userContext = userContext;
        _storageRoot = configuration.GetValue<string>("Storage:Root")
            ?? Path.Combine(AppContext.BaseDirectory, "app_data", "storage", "attachments");

        Directory.CreateDirectory(_storageRoot);
    }

    public async Task<Attachment> StoreAsync(StoreAttachmentRequest request, CancellationToken cancellationToken = default)
    {
        // Compute SHA-256 hash and buffer stream
        using var sha256 = SHA256.Create();
        using var ms = new MemoryStream();
        await request.ContentStream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        var hashBytes = await sha256.ComputeHashAsync(ms, cancellationToken);
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
        ms.Position = 0;

        // Deduplication (Section 3.6): determine storage path based on hash
        var extension = Path.GetExtension(request.FileName);
        var relativePath = $"{hashString}{extension}";
        var physicalPath = Path.Combine(_storageRoot, relativePath);

        // Only write to disk if identical file content is not already saved
        if (!File.Exists(physicalPath))
        {
            using var fileStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await ms.CopyToAsync(fileStream, cancellationToken);
        }

        var attachment = new Attachment
        {
            OwnerType = request.OwnerType,
            OwnerId = request.OwnerId,
            FileName = Path.GetFileName(request.FileName),
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            Size = ms.Length,
            Sha256 = hashString,
            StoragePath = relativePath,
            Visibility = request.Visibility,
            UploadedBy = _userContext.UserId ?? Guid.Empty
        };

        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync(cancellationToken);

        return attachment;
    }

    public async Task<Stream?> OpenReadAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
        if (attachment == null) return null;

        var physicalPath = Path.Combine(_storageRoot, attachment.StoragePath);
        if (!File.Exists(physicalPath)) return null;

        return new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public async Task<bool> DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
        if (attachment == null) return false;

        _db.Attachments.Remove(attachment); // soft delete
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<Attachment>> GetAttachmentsByOwnerAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await _db.Attachments
            .Where(a => a.OwnerType == ownerType && a.OwnerId == ownerId)
            .OrderByDescending(a => a.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
