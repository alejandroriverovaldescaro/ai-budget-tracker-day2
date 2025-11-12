namespace BudgetTracker.Api.Documents;

public interface IDocumentValidator
{
    Task<DocumentValidationResult> ValidateDocumentAsync(
        Stream fileStream,
        string fileName,
        DocumentType expectedType,
        CancellationToken cancellationToken = default);
}
