namespace Ivan.Application.Services;

public interface IAuditService
{
    Task LogAsync(string action, string resourceType, string? resourceId, string summary, CancellationToken cancellationToken = default);
}
