using System.Security.Claims;
using BudgetTracker.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Api.Documents;

public static class DocumentApi
{
    public static RouteGroupBuilder MapDocumentEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/documents/validate", ValidateDocument)
            .DisableAntiforgery()
            .RequireAuthorization();

        return group;
    }

    private static async Task<IResult> ValidateDocument(
        [FromForm] IFormFile file,
        [FromForm] DocumentType expectedType,
        IDocumentValidator validator,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "No file was uploaded." });
        }

        const long maxFileSize = 10 * 1024 * 1024; // 10MB
        if (file.Length > maxFileSize)
        {
            return Results.BadRequest(new { error = "File size exceeds the maximum allowed size of 10MB." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var result = await validator.ValidateDocumentAsync(
                stream,
                file.FileName,
                expectedType,
                cancellationToken);

            var documentId = Guid.NewGuid().ToString();
            var response = new UploadDocumentResponse(documentId, result);

            return Results.Ok(response);
        }
        catch
        {
            return Results.Problem(
                detail: "An error occurred while processing the document.",
                statusCode: 500);
        }
    }
}
