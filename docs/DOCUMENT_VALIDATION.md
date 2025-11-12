# Document Validation Feature

This feature provides document upload and validation capabilities, including OCR-based text extraction from images and PDFs, automatic document type detection, and expiration date validation.

## Overview

The document validation feature allows you to:
- Upload PDF and image files (JPG, PNG, TIFF, BMP)
- Validate document type matches expected type
- Extract text from PDFs and images using OCR
- Detect and validate expiration dates for identity documents
- Get detailed validation feedback with issues and warnings

## Supported Document Types

- **Passport** - Validates passport number format and expiration date
- **Driver License** - Validates license number and expiration date
- **Identity Card** - Basic validation
- **Utility Bill** - Validates bill date (must be within 90 days)
- **Bank Statement** - Basic validation
- **Other** - Generic document type

## API Endpoint

### POST `/api/documents/validate`

Validates an uploaded document against an expected document type.

**Authentication Required:** Yes (JWT Bearer token or API Key)

**Request:**
- Content-Type: `multipart/form-data`
- Fields:
  - `file`: The document file (PDF or image)
  - `expectedType`: The expected document type (e.g., "Passport", "DriverLicense")

**File Requirements:**
- Maximum file size: 10 MB
- Supported formats: PDF, JPG, JPEG, PNG, TIFF, TIF, BMP
- Recommended minimum image resolution: 300x300 pixels for better OCR results

**Response:**

```json
{
  "documentId": "a1b2c3d4-e5f6-7890-1234-567890abcdef",
  "validationResult": {
    "isValid": true,
    "documentType": "Passport",
    "issues": [
      {
        "field": "ExpirationDate",
        "message": "Passport will expire soon on 2025-06-15",
        "status": "Warning"
      }
    ],
    "extractedData": {
      "PassportNumber": "AB123456",
      "ExpirationDate": "2025-06-15",
      "DetectedType": "Passport",
      "FullText": "..."
    }
  }
}
```

**Validation Statuses:**
- `Valid` - Document passes validation
- `Warning` - Document has issues but may still be acceptable (e.g., expiring soon)
- `Invalid` - Document fails validation (e.g., expired)

## Usage Examples

### Using cURL

```bash
# Using API Key authentication
curl -X POST "https://localhost:5295/api/documents/validate" \
  -H "X-API-Key: your-api-key" \
  -F "file=@passport.pdf" \
  -F "expectedType=Passport"

# Using Bearer token authentication
curl -X POST "https://localhost:5295/api/documents/validate" \
  -H "Authorization: Bearer your-jwt-token" \
  -F "file=@passport.jpg" \
  -F "expectedType=Passport"
```

### Using C# HttpClient

```csharp
using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = 
    new AuthenticationHeaderValue("Bearer", token);

using var content = new MultipartFormDataContent();
using var fileStream = File.OpenRead("passport.pdf");
var fileContent = new StreamContent(fileStream);
fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

content.Add(fileContent, "file", "passport.pdf");
content.Add(new StringContent("Passport"), "expectedType");

var response = await client.PostAsync(
    "https://localhost:5295/api/documents/validate", 
    content);

var result = await response.Content.ReadFromJsonAsync<UploadDocumentResponse>();
```

### Using JavaScript/TypeScript

```typescript
const formData = new FormData();
formData.append('file', file);
formData.append('expectedType', 'Passport');

const response = await fetch('/api/documents/validate', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`
  },
  body: formData
});

const result = await response.json();
```

## Validation Rules

### Passport Validation
- Checks for passport number pattern (e.g., AB123456)
- Validates expiration date is in the future
- Warns if expiration date is within 6 months
- Marks as invalid if passport is expired

### Driver License Validation
- Checks for license number pattern
- Validates expiration date is in the future
- Marks as invalid if license is expired

### Utility Bill Validation
- Checks for bill date
- Marks as invalid if bill is older than 90 days

### Document Type Detection
The system attempts to automatically detect document type based on:
- Text keywords (e.g., "passport", "driver license")
- Number patterns (e.g., passport number format)
- Document structure

If detected type differs from expected type, a warning is issued.

## OCR Configuration

The feature uses Tesseract OCR for text extraction from images. 

**Configuration in appsettings.json:**
```json
{
  "TesseractDataPath": "./tessdata"
}
```

**Installing Tesseract Language Data:**

The Tesseract language data files are required for OCR to work. Download the English language data file:

1. Download `eng.traineddata` from: https://github.com/tesseract-ocr/tessdata
2. Place it in the `tessdata` directory relative to the application
3. Ensure the directory structure is: `./tessdata/eng.traineddata`

## Error Handling

The API returns appropriate HTTP status codes:

- **200 OK** - Document processed successfully (even if validation fails)
- **400 Bad Request** - No file uploaded or file exceeds size limit
- **401 Unauthorized** - Authentication required
- **500 Internal Server Error** - Processing error

## Best Practices

1. **Image Quality**: For best OCR results, use high-resolution scans (at least 300 DPI)
2. **File Format**: PDF files often provide better text extraction than images
3. **Document Orientation**: Ensure documents are properly oriented (not upside down)
4. **Lighting**: For scanned images, ensure good lighting and contrast
5. **File Size**: Compress large images before upload to stay within the 10MB limit

## Limitations

1. OCR accuracy depends on image quality and document clarity
2. Handwritten text may not be recognized accurately
3. Complex layouts or multi-column documents may have text extraction issues
4. Date formats are expected to be in common formats (DD/MM/YYYY, MM/DD/YYYY, etc.)
5. Document type detection is based on heuristics and may not be 100% accurate

## Security Considerations

1. All uploaded files are processed in memory and not stored permanently
2. Authentication is required for all document validation requests
3. File size limits prevent abuse and resource exhaustion
4. Supported file types are restricted to prevent malicious uploads

## Future Enhancements

Potential improvements for this feature:
- Store validated documents in a database
- Support for more document types (tax forms, employment letters, etc.)
- Multi-language OCR support
- Enhanced date format detection
- Integration with AI services for improved accuracy
- Document quality scoring
- Barcode/QR code extraction
