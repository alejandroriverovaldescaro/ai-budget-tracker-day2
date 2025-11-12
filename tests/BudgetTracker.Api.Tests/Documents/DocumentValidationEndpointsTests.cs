using System.Net;
using System.Net.Http.Headers;
using BudgetTracker.Api.Documents;
using BudgetTracker.Api.Tests.Fixtures;

namespace BudgetTracker.Api.Tests.Documents;

[Collection("Database")]
public class DocumentValidationEndpointsTests
{
    private readonly ApiFixture _fixture;

    public DocumentValidationEndpointsTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Should_return_unauthorized_when_not_authenticated()
    {
        var client = _fixture.CreateClient();
        
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(DocumentType.Passport.ToString()), "expectedType");
        
        var response = await client.PostAsync("/api/documents/validate", content, TestContext.Current.CancellationToken);
        
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Should_return_bad_request_when_no_file_uploaded()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(DocumentType.Passport.ToString()), "expectedType");
        
        var response = await client.PostAsync("/api/documents/validate", content, TestContext.Current.CancellationToken);
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Should_validate_pdf_file_successfully()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        
        var pdfContent = CreateSamplePdfBytes();
        using var content = new MultipartFormDataContent();
        
        var fileContent = new ByteArrayContent(pdfContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "test.pdf");
        content.Add(new StringContent(DocumentType.Passport.ToString()), "expectedType");
        
        var response = await client.PostAsync("/api/documents/validate", content, TestContext.Current.CancellationToken);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Should_validate_image_file_successfully()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        
        var imageContent = CreateSamplePngBytes();
        using var content = new MultipartFormDataContent();
        
        var fileContent = new ByteArrayContent(imageContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");
        content.Add(new StringContent(DocumentType.Passport.ToString()), "expectedType");
        
        var response = await client.PostAsync("/api/documents/validate", content, TestContext.Current.CancellationToken);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Should_reject_file_exceeding_size_limit()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        
        var largeContent = new byte[11 * 1024 * 1024]; // 11MB
        using var content = new MultipartFormDataContent();
        
        var fileContent = new ByteArrayContent(largeContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "large.pdf");
        content.Add(new StringContent(DocumentType.Passport.ToString()), "expectedType");
        
        var response = await client.PostAsync("/api/documents/validate", content, TestContext.Current.CancellationToken);
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private byte[] CreateSamplePdfBytes()
    {
        return "%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n3 0 obj\n<< /Type /Page /Parent 2 0 R /Resources 4 0 R /MediaBox [0 0 612 792] /Contents 5 0 R >>\nendobj\n4 0 obj\n<< /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >>\nendobj\n5 0 obj\n<< /Length 44 >>\nstream\nBT\n/F1 12 Tf\n100 700 Td\n(Passport) Tj\nET\nendstream\nendobj\nxref\n0 6\n0000000000 65535 f\n0000000009 00000 n\n0000000058 00000 n\n0000000115 00000 n\n0000000234 00000 n\n0000000326 00000 n\ntrailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n419\n%%EOF"u8.ToArray();
    }

    private byte[] CreateSamplePngBytes()
    {
        return new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
            0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
            0x42, 0x60, 0x82
        };
    }
}
