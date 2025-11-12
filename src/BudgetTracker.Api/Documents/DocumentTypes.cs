namespace BudgetTracker.Api.Documents;

public enum DocumentType
{
    Passport,
    DriverLicense,
    IdentityCard,
    UtilityBill,
    BankStatement,
    Other
}

public enum ValidationStatus
{
    Valid,
    Invalid,
    Warning
}

public record ValidationIssue(
    string Field,
    string Message,
    ValidationStatus Status
);

public record DocumentValidationResult(
    bool IsValid,
    string? DocumentType,
    List<ValidationIssue> Issues,
    Dictionary<string, string> ExtractedData
);

public record UploadDocumentRequest(
    IFormFile File,
    DocumentType ExpectedDocumentType
);

public record UploadDocumentResponse(
    string DocumentId,
    DocumentValidationResult ValidationResult
);
