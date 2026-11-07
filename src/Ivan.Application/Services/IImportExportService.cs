namespace Ivan.Application.Services;

public record CsvImportResultDto(
    int ProcessedRows,
    int SucceededRows,
    int FailedRows,
    IReadOnlyList<string> Errors);

public interface IImportExportService
{
    Task<CsvImportResultDto> ImportBooksCsvAsync(Stream csvStream, CancellationToken ct = default);
    Task<CsvImportResultDto> ImportAssetsCsvAsync(Stream csvStream, CancellationToken ct = default);
    Task<CsvImportResultDto> ImportTransactionsCsvAsync(Stream csvStream, CancellationToken ct = default);
    Task<string> ExportHouseholdJsonAsync(Guid householdId, CancellationToken ct = default);
}
