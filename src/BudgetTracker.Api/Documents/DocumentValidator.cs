using System.Globalization;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tesseract;
using UglyToad.PdfPig;

namespace BudgetTracker.Api.Documents;

public class DocumentValidator : IDocumentValidator
{
    private readonly ILogger<DocumentValidator> _logger;
    private readonly string _tessDataPath;

    public DocumentValidator(ILogger<DocumentValidator> logger, IConfiguration configuration)
    {
        _logger = logger;
        _tessDataPath = configuration["TesseractDataPath"] ?? "./tessdata";
    }

    public async Task<DocumentValidationResult> ValidateDocumentAsync(
        Stream fileStream,
        string fileName,
        DocumentType expectedType,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<ValidationIssue>();
        var extractedData = new Dictionary<string, string>();
        string? detectedDocumentType = null;

        try
        {
            var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            
            if (fileExtension == ".pdf")
            {
                var pdfValidation = await ValidatePdfAsync(fileStream, expectedType, cancellationToken);
                issues.AddRange(pdfValidation.Issues);
                foreach (var kvp in pdfValidation.ExtractedData)
                {
                    extractedData[kvp.Key] = kvp.Value;
                }
                detectedDocumentType = pdfValidation.DocumentType;
            }
            else if (IsImageFile(fileExtension))
            {
                var imageValidation = await ValidateImageAsync(fileStream, expectedType, cancellationToken);
                issues.AddRange(imageValidation.Issues);
                foreach (var kvp in imageValidation.ExtractedData)
                {
                    extractedData[kvp.Key] = kvp.Value;
                }
                detectedDocumentType = imageValidation.DocumentType;
            }
            else
            {
                issues.Add(new ValidationIssue(
                    "FileType",
                    $"Unsupported file type: {fileExtension}. Please upload a PDF or image file (JPG, PNG, TIFF).",
                    ValidationStatus.Invalid
                ));
            }

            if (!string.IsNullOrEmpty(detectedDocumentType) && 
                detectedDocumentType != expectedType.ToString())
            {
                issues.Add(new ValidationIssue(
                    "DocumentType",
                    $"Expected {expectedType} but detected {detectedDocumentType}",
                    ValidationStatus.Warning
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating document {FileName}", fileName);
            issues.Add(new ValidationIssue(
                "Processing",
                $"Error processing document: {ex.Message}",
                ValidationStatus.Invalid
            ));
        }

        var isValid = !issues.Any(i => i.Status == ValidationStatus.Invalid);
        return new DocumentValidationResult(isValid, detectedDocumentType, issues, extractedData);
    }

    private async Task<DocumentValidationResult> ValidatePdfAsync(
        Stream fileStream,
        DocumentType expectedType,
        CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();
        var extractedData = new Dictionary<string, string>();
        string? detectedType = null;

        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        try
        {
            using var document = PdfDocument.Open(memoryStream);
            var allText = string.Join(" ", document.GetPages().Select(p => p.Text));
            extractedData["FullText"] = allText;

            detectedType = DetectDocumentType(allText);
            extractedData["DetectedType"] = detectedType ?? "Unknown";

            switch (expectedType)
            {
                case DocumentType.Passport:
                    ValidatePassport(allText, issues, extractedData);
                    break;
                case DocumentType.DriverLicense:
                    ValidateDriverLicense(allText, issues, extractedData);
                    break;
                case DocumentType.UtilityBill:
                    ValidateUtilityBill(allText, issues, extractedData);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PDF");
            issues.Add(new ValidationIssue(
                "PDF",
                "Failed to process PDF file. The file may be corrupted or password-protected.",
                ValidationStatus.Invalid
            ));
        }

        var isValid = !issues.Any(i => i.Status == ValidationStatus.Invalid);
        return new DocumentValidationResult(isValid, detectedType, issues, extractedData);
    }

    private async Task<DocumentValidationResult> ValidateImageAsync(
        Stream fileStream,
        DocumentType expectedType,
        CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();
        var extractedData = new Dictionary<string, string>();
        string? detectedType = null;

        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        try
        {
            using var image = await Image.LoadAsync<Rgba32>(memoryStream, cancellationToken);
            extractedData["ImageWidth"] = image.Width.ToString();
            extractedData["ImageHeight"] = image.Height.ToString();

            if (image.Width < 300 || image.Height < 300)
            {
                issues.Add(new ValidationIssue(
                    "ImageQuality",
                    "Image resolution is too low. Please upload a higher quality image (minimum 300x300 pixels).",
                    ValidationStatus.Warning
                ));
            }

            var ocrText = await ExtractTextFromImageAsync(image, cancellationToken);
            extractedData["FullText"] = ocrText;

            detectedType = DetectDocumentType(ocrText);
            extractedData["DetectedType"] = detectedType ?? "Unknown";

            switch (expectedType)
            {
                case DocumentType.Passport:
                    ValidatePassport(ocrText, issues, extractedData);
                    break;
                case DocumentType.DriverLicense:
                    ValidateDriverLicense(ocrText, issues, extractedData);
                    break;
                case DocumentType.UtilityBill:
                    ValidateUtilityBill(ocrText, issues, extractedData);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing image");
            issues.Add(new ValidationIssue(
                "Image",
                "Failed to process image file. The file may be corrupted or in an unsupported format.",
                ValidationStatus.Invalid
            ));
        }

        var isValid = !issues.Any(i => i.Status == ValidationStatus.Invalid);
        return new DocumentValidationResult(isValid, detectedType, issues, extractedData);
    }

    private async Task<string> ExtractTextFromImageAsync(Image<Rgba32> image, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_tessDataPath))
        {
            _logger.LogWarning("Tesseract data path not found: {Path}. OCR will be skipped.", _tessDataPath);
            return string.Empty;
        }

        try
        {
            using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
            
            using var memoryStream = new MemoryStream();
            await image.SaveAsPngAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            
            using var pix = Pix.LoadFromMemory(memoryStream.ToArray());
            using var page = engine.Process(pix);
            return page.GetText();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR failed, returning empty text");
            return string.Empty;
        }
    }

    private string? DetectDocumentType(string text)
    {
        var lowerText = text.ToLowerInvariant();

        if (lowerText.Contains("passport") || 
            Regex.IsMatch(text, @"\b[A-Z]{1,2}\d{6,9}\b"))
        {
            return DocumentType.Passport.ToString();
        }

        if (lowerText.Contains("driver") && lowerText.Contains("license") ||
            lowerText.Contains("driving licence"))
        {
            return DocumentType.DriverLicense.ToString();
        }

        if (lowerText.Contains("identity") || lowerText.Contains("id card"))
        {
            return DocumentType.IdentityCard.ToString();
        }

        if (lowerText.Contains("utility") || lowerText.Contains("electric") || 
            lowerText.Contains("water") || lowerText.Contains("gas"))
        {
            return DocumentType.UtilityBill.ToString();
        }

        if (lowerText.Contains("bank") && lowerText.Contains("statement"))
        {
            return DocumentType.BankStatement.ToString();
        }

        return null;
    }

    private void ValidatePassport(string text, List<ValidationIssue> issues, Dictionary<string, string> extractedData)
    {
        var passportNumberMatch = Regex.Match(text, @"\b[A-Z]{1,2}\d{6,9}\b");
        if (passportNumberMatch.Success)
        {
            extractedData["PassportNumber"] = passportNumberMatch.Value;
        }
        else
        {
            issues.Add(new ValidationIssue(
                "PassportNumber",
                "Could not identify passport number in the document.",
                ValidationStatus.Warning
            ));
        }

        var expirationDate = ExtractExpirationDate(text);
        if (expirationDate.HasValue)
        {
            extractedData["ExpirationDate"] = expirationDate.Value.ToString("yyyy-MM-dd");
            
            if (expirationDate.Value < DateTime.Now)
            {
                issues.Add(new ValidationIssue(
                    "ExpirationDate",
                    $"Passport has expired on {expirationDate.Value:yyyy-MM-dd}",
                    ValidationStatus.Invalid
                ));
            }
            else if (expirationDate.Value < DateTime.Now.AddMonths(6))
            {
                issues.Add(new ValidationIssue(
                    "ExpirationDate",
                    $"Passport will expire soon on {expirationDate.Value:yyyy-MM-dd}",
                    ValidationStatus.Warning
                ));
            }
        }
        else
        {
            issues.Add(new ValidationIssue(
                "ExpirationDate",
                "Could not identify expiration date in the passport.",
                ValidationStatus.Warning
            ));
        }
    }

    private void ValidateDriverLicense(string text, List<ValidationIssue> issues, Dictionary<string, string> extractedData)
    {
        var licenseNumberMatch = Regex.Match(text, @"\b[A-Z0-9]{8,16}\b");
        if (licenseNumberMatch.Success)
        {
            extractedData["LicenseNumber"] = licenseNumberMatch.Value;
        }

        var expirationDate = ExtractExpirationDate(text);
        if (expirationDate.HasValue)
        {
            extractedData["ExpirationDate"] = expirationDate.Value.ToString("yyyy-MM-dd");
            
            if (expirationDate.Value < DateTime.Now)
            {
                issues.Add(new ValidationIssue(
                    "ExpirationDate",
                    $"Driver license has expired on {expirationDate.Value:yyyy-MM-dd}",
                    ValidationStatus.Invalid
                ));
            }
        }
    }

    private void ValidateUtilityBill(string text, List<ValidationIssue> issues, Dictionary<string, string> extractedData)
    {
        var dateMatch = ExtractDate(text);
        if (dateMatch.HasValue)
        {
            extractedData["BillDate"] = dateMatch.Value.ToString("yyyy-MM-dd");
            
            var ageInDays = (DateTime.Now - dateMatch.Value).Days;
            if (ageInDays > 90)
            {
                issues.Add(new ValidationIssue(
                    "BillDate",
                    "Utility bill is older than 90 days. Please provide a recent bill.",
                    ValidationStatus.Invalid
                ));
            }
        }
        else
        {
            issues.Add(new ValidationIssue(
                "BillDate",
                "Could not identify bill date.",
                ValidationStatus.Warning
            ));
        }
    }

    private DateTime? ExtractExpirationDate(string text)
    {
        var expiryPatterns = new[]
        {
            @"expir(?:y|ation|es)?\s*:?\s*(\d{1,2})[\/\-\.](\d{1,2})[\/\-\.](\d{2,4})",
            @"valid\s+until\s*:?\s*(\d{1,2})[\/\-\.](\d{1,2})[\/\-\.](\d{2,4})",
            @"exp(?:iry)?\s+date\s*:?\s*(\d{1,2})[\/\-\.](\d{1,2})[\/\-\.](\d{2,4})"
        };

        foreach (var pattern in expiryPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return TryParseDate(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
            }
        }

        return null;
    }

    private DateTime? ExtractDate(string text)
    {
        var datePatterns = new[]
        {
            @"(\d{1,2})[\/\-\.](\d{1,2})[\/\-\.](\d{2,4})",
            @"(\d{2,4})[\/\-\.](\d{1,2})[\/\-\.](\d{1,2})"
        };

        foreach (var pattern in datePatterns)
        {
            var matches = Regex.Matches(text, pattern);
            foreach (Match match in matches)
            {
                var date = TryParseDate(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
                if (date.HasValue && date.Value < DateTime.Now && date.Value > DateTime.Now.AddYears(-1))
                {
                    return date;
                }
            }
        }

        return null;
    }

    private DateTime? TryParseDate(string part1, string part2, string part3)
    {
        var year = int.Parse(part3);
        if (year < 100)
        {
            year += year > 50 ? 1900 : 2000;
        }

        var formats = new[]
        {
            (int.Parse(part2), int.Parse(part1), year),
            (int.Parse(part1), int.Parse(part2), year)
        };

        foreach (var (day, month, y) in formats)
        {
            try
            {
                if (month >= 1 && month <= 12 && day >= 1 && day <= 31)
                {
                    return new DateTime(y, month, day);
                }
            }
            catch
            {
                continue;
            }
        }

        return null;
    }

    private bool IsImageFile(string extension)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".tiff", ".tif", ".bmp" };
        return imageExtensions.Contains(extension);
    }
}
