using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Owezy.Api.Auth;
using Owezy.Application.Receipts;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Owezy.Domain.Receipts;

namespace Owezy.Api.Receipts;

public static class ReceiptEndpoints
{
    public static IEndpointRouteBuilder MapReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/bills")
            .RequireAuthorization();

        group.MapPost("/{billId}/receipt", HandleUploadReceiptAsync)
            .DisableAntiforgery()
            .WithName("UploadReceipt");

        group.MapGet("/{billId}/receipt/{receiptId}", HandleGetReceiptDraftAsync)
            .WithName("GetReceiptDraft");

        group.MapPut("/{billId}/receipt/{receiptId}", HandleUpdateReceiptDraftAsync)
            .WithName("UpdateReceiptDraft");

        group.MapPost("/{billId}/receipt/{receiptId}/confirm", HandleConfirmReceiptAsync)
            .WithName("ConfirmReceipt");

        return app;
    }

    private static async Task<IResult> HandleUploadReceiptAsync(
        string billId,
        IFormFile? file,
        ClaimsPrincipal user,
        IReceiptService receiptService,
        CancellationToken cancellationToken)
    {
        var authenticatedPhone = GetAuthenticatedPhoneNumber(user);
        if (authenticatedPhone is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(billId, out var billGuid))
        {
            return Results.BadRequest(new ApiError("invalid_bill_id", "billId must be a valid GUID."));
        }

        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new ApiError("missing_file", "No receipt image was provided."));
        }

        UploadReceiptResult result;
        try
        {
            await using var stream = file.OpenReadStream();
            result = await receiptService.UploadReceiptAsync(
                authenticatedPhone,
                new BillId(billGuid),
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new ApiError("bill_not_found", "The specified bill was not found."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(new ApiError("unauthorized", ex.Message), statusCode: 403);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("finalized", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(new ApiError("bill_finalized", ex.Message), statusCode: 409);
            }
            return Results.BadRequest(new ApiError("invalid_receipt_file", ex.Message));
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred while processing the receipt image.", statusCode: 500);
        }

        var response = MapToUploadResponse(result);
        return Results.Created($"/bills/{result.BillId}/receipt/{result.ReceiptId}", response);
    }

    private static async Task<IResult> HandleGetReceiptDraftAsync(
        string billId,
        string receiptId,
        ClaimsPrincipal user,
        IReceiptService receiptService,
        CancellationToken cancellationToken)
    {
        var authenticatedPhone = GetAuthenticatedPhoneNumber(user);
        if (authenticatedPhone is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(billId, out var billGuid))
        {
            return Results.BadRequest(new ApiError("invalid_bill_id", "billId must be a valid GUID."));
        }

        if (!Guid.TryParse(receiptId, out var receiptGuid))
        {
            return Results.BadRequest(new ApiError("invalid_receipt_id", "receiptId must be a valid GUID."));
        }

        ReceiptDraftResult? result;
        try
        {
            result = await receiptService.GetReceiptDraftAsync(
                authenticatedPhone,
                new BillId(billGuid),
                new ReceiptId(receiptGuid),
                cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(new ApiError("unauthorized", ex.Message), statusCode: 403);
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred while retrieving the receipt draft.", statusCode: 500);
        }

        if (result is null)
        {
            return Results.NotFound(new ApiError("receipt_not_found", "The specified receipt draft was not found."));
        }

        var response = MapToDraftResponse(result);
        return Results.Ok(response);
    }

    private static UploadReceiptHttpResponse MapToUploadResponse(UploadReceiptResult result)
    {
        return new UploadReceiptHttpResponse(
            result.ReceiptId.Value.ToString(),
            result.BillId.Value.ToString(),
            result.Status.ToString(),
            result.CreatedAt,
            MapDraft(result.OcrDraft)
        );
    }

    private static ReceiptDraftHttpResponse MapToDraftResponse(ReceiptDraftResult result)
    {
        return new ReceiptDraftHttpResponse(
            result.ReceiptId.Value.ToString(),
            result.BillId.Value.ToString(),
            result.Status.ToString(),
            result.CreatedAt,
            MapDraft(result.OcrDraft)
        );
    }

    private static OcrReceiptDraftHttpResponse? MapDraft(OcrReceiptDraft? draft)
    {
        if (draft is null) return null;

        return new OcrReceiptDraftHttpResponse(
            draft.MerchantName,
            draft.ReceiptDate,
            draft.Currency,
            draft.Subtotal,
            draft.Tax,
            draft.Discount,
            draft.Total,
            draft.LineItems.Select(i => new OcrLineItemHttpResponse(
                i.Description,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal,
                i.IsLineTotalDerived,
                i.Confidence
            )).ToList()
        );
    }

    private static async Task<IResult> HandleUpdateReceiptDraftAsync(
        string billId,
        string receiptId,
        UpdateReceiptDraftHttpRequest request,
        ClaimsPrincipal user,
        IReceiptService receiptService,
        CancellationToken cancellationToken)
    {
        var authenticatedPhone = GetAuthenticatedPhoneNumber(user);
        if (authenticatedPhone is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(billId, out var billGuid))
        {
            return Results.BadRequest(new ApiError("invalid_bill_id", "billId must be a valid GUID."));
        }

        if (!Guid.TryParse(receiptId, out var receiptGuid))
        {
            return Results.BadRequest(new ApiError("invalid_receipt_id", "receiptId must be a valid GUID."));
        }

        if (request is null)
        {
            return Results.BadRequest(new ApiError("invalid_request", "Request body cannot be null."));
        }

        var updateReq = new UpdateReceiptDraftRequest(
            request.MerchantName,
            request.ReceiptDate,
            request.Currency,
            request.Subtotal,
            request.Tax,
            request.Discount,
            request.Total,
            request.LineItems?.Select(i => new OcrLineItemDto(
                i.Description ?? string.Empty,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal,
                i.Confidence
            )).ToList() ?? new List<OcrLineItemDto>()
        );

        ReceiptDraftResult? result;
        try
        {
            result = await receiptService.UpdateReceiptDraftAsync(
                authenticatedPhone,
                new BillId(billGuid),
                new ReceiptId(receiptGuid),
                updateReq,
                cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(new ApiError("unauthorized", ex.Message), statusCode: 403);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(new ApiError("invalid_operation", ex.Message), statusCode: 409);
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred while updating the receipt draft.", statusCode: 500);
        }

        if (result is null)
        {
            return Results.NotFound(new ApiError("receipt_not_found", "The specified receipt draft was not found."));
        }

        var response = MapToDraftResponse(result);
        return Results.Ok(response);
    }

    private static async Task<IResult> HandleConfirmReceiptAsync(
        string billId,
        string receiptId,
        ClaimsPrincipal user,
        IReceiptService receiptService,
        CancellationToken cancellationToken)
    {
        var authenticatedPhone = GetAuthenticatedPhoneNumber(user);
        if (authenticatedPhone is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(billId, out var billGuid))
        {
            return Results.BadRequest(new ApiError("invalid_bill_id", "billId must be a valid GUID."));
        }

        if (!Guid.TryParse(receiptId, out var receiptGuid))
        {
            return Results.BadRequest(new ApiError("invalid_receipt_id", "receiptId must be a valid GUID."));
        }

        ConfirmReceiptResult result;
        try
        {
            result = await receiptService.ConfirmReceiptAsync(
                authenticatedPhone,
                new BillId(billGuid),
                new ReceiptId(receiptGuid),
                cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new ApiError("receipt_not_found", ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(new ApiError("unauthorized", ex.Message), statusCode: 403);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(new ApiError("confirmation_failed", ex.Message), statusCode: 409);
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred while confirming the receipt draft.", statusCode: 500);
        }

        var response = new ConfirmReceiptHttpResponse(
            result.ReceiptId.Value.ToString(),
            result.BillId.Value.ToString(),
            result.ConfirmedAt,
            result.CreatedItemIds.Select(id => id.Value.ToString()).ToList()
        );

        return Results.Ok(response);
    }

    private static PhoneNumber? GetAuthenticatedPhoneNumber(ClaimsPrincipal user)
    {
        var phoneString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst("phone_number")?.Value;

        if (string.IsNullOrWhiteSpace(phoneString))
        {
            return null;
        }

        try
        {
            return PhoneNumber.Create(phoneString);
        }
        catch
        {
            return null;
        }
    }
}
