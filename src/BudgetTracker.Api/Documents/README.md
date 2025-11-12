# Documents Feature

This folder contains the document validation feature implementation.

## Architecture

The document validation feature follows a clean architecture pattern with clear separation of concerns:

### Components

1. **DocumentTypes.cs** - Domain models and DTOs
   - `DocumentType` enum - Supported document types
   - `ValidationStatus` enum - Validation result statuses
   - `ValidationIssue` record - Represents a validation issue
   - `DocumentValidationResult` record - Complete validation result
   - Request/Response DTOs for API

2. **IDocumentValidator.cs** - Service interface
   - Defines the contract for document validation
   - Allows for easy testing and mocking

3. **DocumentValidator.cs** - Core implementation
   - Handles PDF text extraction using PdfPig
   - Handles image processing using ImageSharp
   - Performs OCR using Tesseract
   - Implements validation rules for each document type
   - Extracts and validates expiration dates

4. **DocumentApi.cs** - API endpoints
   - Defines minimal API endpoints
   - Handles file uploads
   - Validates file size limits
   - Returns structured validation results

## Flow

1. Client uploads a file via POST `/api/documents/validate`
2. API validates file size and presence
3. File stream is passed to `DocumentValidator`
4. Validator determines file type (PDF or image)
5. Text is extracted from the document
6. Document type is detected from content
7. Type-specific validation rules are applied
8. Validation result is returned to client

## Dependencies

- **Tesseract 5.2.0** - OCR engine for text extraction from images
- **SixLabors.ImageSharp 3.1.12** - Image processing library
- **PdfPig 0.1.9** - PDF parsing and text extraction

## Configuration

The service requires Tesseract language data files. Configure the path in `appsettings.json`:

```json
{
  "TesseractDataPath": "./tessdata"
}
```

## Testing

Integration tests are located in `tests/BudgetTracker.Api.Tests/Documents/`:
- Tests authentication requirement
- Tests file upload validation
- Tests PDF document processing
- Tests image document processing
- Tests file size limits

## Extension Points

To add a new document type:

1. Add to the `DocumentType` enum in `DocumentTypes.cs`
2. Add detection logic in `DetectDocumentType()` method
3. Create a new validation method (e.g., `ValidateNewType()`)
4. Call the validation method from the main validation logic

Example:
```csharp
case DocumentType.TaxForm:
    ValidateTaxForm(text, issues, extractedData);
    break;
```
