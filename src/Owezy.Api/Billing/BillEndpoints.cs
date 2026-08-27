using System.Security.Claims;
using Owezy.Api.Auth;
using Owezy.Application.Billing;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;

namespace Owezy.Api.Billing;

public static class BillEndpoints
{
    public static IEndpointRouteBuilder MapBillEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/bills")
            .RequireAuthorization();

        group.MapPost("/", HandleCreateBillAsync)
            .WithName("CreateBill");

        group.MapPost("/{billId}/participants", HandleAddParticipantAsync)
            .WithName("AddParticipant");

        group.MapPost("/{billId}/items", HandleAddBillItemAsync)
            .WithName("AddBillItem");

        group.MapPost("/{billId}/finalize", HandleFinalizeBillAsync)
            .WithName("FinalizeBill");

        group.MapPost("/{billId}/participants/{participantId}/access-link", HandleGenerateAccessLinkAsync)
            .WithName("GenerateParticipantAccessLink");

        group.MapGet("/{billId}/payments", HandleGetSplitterBillPaymentsAsync)
            .WithName("GetSplitterBillPayments");

        group.MapGet("/{billId}/settlement", HandleGetBillSettlementAsync)
            .WithName("GetBillSettlement");

        group.MapPut("/{billId}/items/{itemId}/sharers", HandleUpdateItemSharersAsync)
            .WithName("UpdateItemSharers");

        group.MapGet("/{billId}", HandleGetSplitterBillSummaryAsync)
            .WithName("GetSplitterBillSummary");

        app.MapGet("/participant-access/{token}", HandleGetParticipantViewAsync)
            .AllowAnonymous()
            .WithName("GetParticipantView");

        app.MapPost("/participant-access/{token}/payment", HandleMarkParticipantPaidByTokenAsync)
            .AllowAnonymous()
            .WithName("MarkParticipantPaidByToken");

        app.MapGet("/participant-access/{token}/summary", HandleGetParticipantSummaryAsync)
            .AllowAnonymous()
            .WithName("GetParticipantSummary");

        return app;
    }

    private static async Task<IResult> HandleCreateBillAsync(
        CreateBillHttpRequest httpRequest,
        ClaimsPrincipal user,
        IBillService billService,
        CancellationToken cancellationToken)
    {
        var authenticatedPhone = GetAuthenticatedPhoneNumber(user);
        if (authenticatedPhone is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(httpRequest.Title))
        {
            return Results.BadRequest(new ApiError("invalid_request", "Title is required."));
        }

        CreateBillResult result;
        try
        {
            result = await billService.CreateBillAsync(
                authenticatedPhone,
                new CreateBillRequest(httpRequest.Title),
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ApiError("invalid_request", ex.Message));
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred while creating the bill.", statusCode: 500);
        }

        var response = new CreateBillHttpResponse(
            result.BillId.Value.ToString(),
            result.Title,
            result.SplitterPhoneNumber.Value,
            result.ParticipantCount,
            result.CreatedAt
        );

        return Results.Created($"/bills/{result.BillId.Value}", response);
    }

    private static async Task<IResult> HandleAddParticipantAsync(
        string billId,
        AddParticipantHttpRequest httpRequest,
        ClaimsPrincipal user,
        IBillService billService,
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

        if (string.IsNullOrWhiteSpace(httpRequest.PhoneNumber))
        {
            return Results.BadRequest(new ApiError("invalid_request", "phoneNumber is required."));
        }

        PhoneNumber participantPhone;
        try
        {
            participantPhone = PhoneNumber.Create(httpRequest.PhoneNumber);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new ApiError("invalid_phone_number", "The provided phone number is not valid E.164."));
        }

        AddParticipantResult result;
        try
        {
            result = await billService.AddParticipantAsync(
                authenticatedPhone,
                new AddParticipantRequest(new BillId(billGuid), participantPhone),
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
            return Results.Json(new ApiError("duplicate_participant", ex.Message), statusCode: 409);
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred while adding the participant.", statusCode: 500);
        }

        var response = new AddParticipantHttpResponse(
            result.ParticipantId.Value.ToString(),
            result.BillId.Value.ToString(),
            result.PhoneNumber.Value,
            result.JoinedAt
        );

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleAddBillItemAsync(
        string billId,
        AddBillItemHttpRequest httpRequest,
        ClaimsPrincipal user,
        IBillService billService,
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

        if (string.IsNullOrWhiteSpace(httpRequest.Description))
        {
            return Results.BadRequest(new ApiError("invalid_request", "Description is required."));
        }

        if (httpRequest.Quantity <= 0)
        {
            return Results.BadRequest(new ApiError("invalid_quantity", "Quantity must be greater than zero."));
        }

        if (httpRequest.Amount <= 0m)
        {
            return Results.BadRequest(new ApiError("invalid_amount", "Amount must be greater than zero."));
        }

        var sharerGuids = new List<ParticipantId>();
        foreach (var idStr in httpRequest.SharerParticipantIds ?? [])
        {
            if (!Guid.TryParse(idStr, out var sharerGuid))
            {
                return Results.BadRequest(new ApiError("invalid_participant_id", $"Sharer participant ID '{idStr}' is not a valid GUID."));
            }
            sharerGuids.Add(new ParticipantId(sharerGuid));
        }

        AddBillItemResult result;
        try
        {
            result = await billService.AddBillItemAsync(
                authenticatedPhone,
                new AddBillItemRequest(
                    new BillId(billGuid),
                    httpRequest.Description,
                    httpRequest.Quantity,
                    httpRequest.Amount,
                    sharerGuids
                ),
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
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ApiError("invalid_request", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(new ApiError("bill_finalized", ex.Message), statusCode: 409);
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred while adding the item to the bill.", statusCode: 500);
        }

        var response = new AddBillItemHttpResponse(
            result.ItemId.Value.ToString(),
            result.BillId.Value.ToString(),
            result.Description,
            result.Quantity,
            result.Amount,
            result.SharerParticipantIds.Select(s => s.Value.ToString()).ToList()
        );

        return Results.Created($"/bills/{result.BillId.Value}/items/{result.ItemId.Value}", response);
    }

    private static async Task<IResult> HandleFinalizeBillAsync(
        string billId,
        ClaimsPrincipal user,
        IBillService billService,
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

        FinalizeBillResult result;
        try
        {
            result = await billService.FinalizeBillAsync(
                authenticatedPhone,
                new FinalizeBillRequest(new Domain.Billing.BillId(billGuid)),
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
            return Results.Json(new ApiError("bill_finalization_failed", ex.Message), statusCode: 409);
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred while finalizing the bill.", statusCode: 500);
        }

        var response = new FinalizeBillHttpResponse(
            result.BillId.Value.ToString(),
            result.Title,
            result.Status.ToString(),
            result.FinalizedAt
        );

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleGenerateAccessLinkAsync(
        string billId,
        string participantId,
        ClaimsPrincipal user,
        IBillService billService,
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

        if (!Guid.TryParse(participantId, out var participantGuid))
        {
            return Results.BadRequest(new ApiError("invalid_participant_id", "participantId must be a valid GUID."));
        }

        GenerateParticipantAccessLinkResult result;
        try
        {
            result = await billService.GenerateParticipantAccessLinkAsync(
                authenticatedPhone,
                new GenerateParticipantAccessLinkRequest(new BillId(billGuid), new ParticipantId(participantGuid)),
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
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ApiError("invalid_request", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(new ApiError("bill_not_finalized", ex.Message), statusCode: 409);
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred while generating the participant access link.", statusCode: 500);
        }

        var response = new GenerateAccessLinkHttpResponse(
            result.RawToken,
            result.BillId.Value.ToString(),
            result.ParticipantId.Value.ToString()
        );

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleGetParticipantViewAsync(
        string token,
        IBillService billService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.NotFound(new ApiError("invalid_token", "Participant access token is invalid or expired."));
        }

        var result = await billService.GetParticipantViewAsync(token, cancellationToken);
        if (result is null)
        {
            return Results.NotFound(new ApiError("access_denied", "Participant access token is invalid or expired."));
        }

        var response = new ParticipantBillViewHttpResponse(
            result.BillTitle,
            result.BillTotalAmount,
            result.ParticipantId.Value.ToString(),
            result.ParticipantPhoneNumber.Value,
            result.TotalAmountOwed,
            result.PaymentStatus.ToString(),
            result.PaidAt,
            result.Items.Select(i => new ParticipantItemShareHttpResponse(
                i.Description,
                i.Quantity,
                i.ItemTotalAmount,
                i.MyShareAmount
            )).ToList()
        );

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleMarkParticipantPaidByTokenAsync(
        string token,
        IBillService billService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.NotFound(new ApiError("invalid_token", "Participant access token is invalid or expired."));
        }

        var result = await billService.MarkParticipantPaidByTokenAsync(token, cancellationToken);
        if (result is null)
        {
            return Results.NotFound(new ApiError("access_denied", "Participant access token is invalid or expired."));
        }

        var response = new MarkParticipantPaidHttpResponse(
            result.ParticipantId.Value.ToString(),
            result.PaymentStatus.ToString(),
            result.PaidAt
        );

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleGetSplitterBillPaymentsAsync(
        string billId,
        ClaimsPrincipal user,
        IBillService billService,
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

        SplitterBillPaymentsResult result;
        try
        {
            result = await billService.GetSplitterBillPaymentsAsync(
                authenticatedPhone,
                new BillId(billGuid),
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
            return Results.Json(new ApiError("bill_not_finalized", ex.Message), statusCode: 409);
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred while retrieving payment statuses.", statusCode: 500);
        }

        var response = new SplitterBillPaymentsHttpResponse(
            result.BillId.Value.ToString(),
            result.BillTitle,
            result.BillTotalAmount,
            result.ParticipantPayments.Select(p => new ParticipantPaymentStatusHttpResponse(
                p.ParticipantId.Value.ToString(),
                p.PhoneNumber.Value,
                p.AmountOwed,
                p.PaymentStatus.ToString(),
                p.PaidAt
            )).ToList()
        );

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleGetBillSettlementAsync(
        string billId,
        ClaimsPrincipal user,
        IBillService billService,
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

        BillSettlementResult result;
        try
        {
            result = await billService.GetBillSettlementAsync(
                authenticatedPhone,
                new BillId(billGuid),
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
            return Results.Json(new ApiError("bill_not_finalized", ex.Message), statusCode: 409);
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred while retrieving the settlement summary.", statusCode: 500);
        }

        var response = new BillSettlementHttpResponse(
            result.BillId.Value.ToString(),
            result.BillTitle,
            result.BillTotalAmount,
            result.TotalOwed,
            result.TotalPaid,
            result.TotalRemaining,
            result.ParticipantCount,
            result.PaidCount,
            result.UnpaidCount,
            result.Participants.Select(p => new ParticipantSettlementHttpResponse(
                p.ParticipantId.Value.ToString(),
                p.PhoneNumber.Value,
                p.AmountOwed,
                p.AmountPaid,
                p.AmountRemaining,
                p.PaymentStatus.ToString()
            )).ToList()
        );

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleUpdateItemSharersAsync(
        string billId,
        string itemId,
        UpdateItemSharersHttpRequest request,
        ClaimsPrincipal user,
        IBillService billService,
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

        if (!Guid.TryParse(itemId, out var itemGuid))
        {
            return Results.BadRequest(new ApiError("invalid_item_id", "itemId must be a valid GUID."));
        }

        if (request is null || request.ParticipantIds is null)
        {
            return Results.BadRequest(new ApiError("invalid_request", "ParticipantIds list must be provided."));
        }

        var participantGuids = new List<ParticipantId>();
        foreach (var idStr in request.ParticipantIds)
        {
            if (!Guid.TryParse(idStr, out var pGuid))
            {
                return Results.BadRequest(new ApiError("invalid_participant_id", $"Participant ID '{idStr}' is not a valid GUID."));
            }
            participantGuids.Add(new ParticipantId(pGuid));
        }

        UpdateItemSharersResult result;
        try
        {
            result = await billService.UpdateItemSharersAsync(
                authenticatedPhone,
                new UpdateItemSharersRequest(
                    new BillId(billGuid),
                    new BillItemId(itemGuid),
                    participantGuids
                ),
                cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new ApiError("not_found", ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(new ApiError("unauthorized", ex.Message), statusCode: 403);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(new ApiError("invalid_operation", ex.Message), statusCode: 409);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ApiError("invalid_sharers", ex.Message));
        }
        catch (Exception)
        {
            return Results.Problem("An error occurred while updating item sharers.", statusCode: 500);
        }

        var response = new UpdateItemSharersHttpResponse(
            result.ItemId.Value.ToString(),
            result.BillId.Value.ToString(),
            result.SharerParticipantIds.Select(id => id.Value.ToString()).ToList()
        );

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleGetSplitterBillSummaryAsync(
        string billId,
        ClaimsPrincipal user,
        IBillService billService,
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

        SplitterBillSummaryResult result;
        try
        {
            result = await billService.GetSplitterBillSummaryAsync(
                authenticatedPhone,
                new BillId(billGuid),
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
        catch (Exception)
        {
            return Results.Problem("An error occurred while retrieving the bill summary.", statusCode: 500);
        }

        var response = new SplitterBillSummaryHttpResponse(
            result.BillId.Value.ToString(),
            result.Title,
            result.SplitterPhoneNumber.Value,
            result.Status.ToString(),
            result.CreatedAt,
            result.FinalizedAt,
            result.TotalAmount,
            result.Participants.Select(p => new BillSummaryParticipantHttpResponse(
                p.ParticipantId.Value.ToString(),
                p.PhoneNumber.Value,
                p.AmountOwed,
                p.PaymentStatus.ToString(),
                p.PaidAt
            )).ToList(),
            result.Items.Select(i => new BillSummaryItemHttpResponse(
                i.ItemId.Value.ToString(),
                i.Description,
                i.Quantity,
                i.Amount,
                i.SharerParticipantIds.Select(s => s.Value.ToString()).ToList(),
                i.CalculatedShares.Select(s => new BillSummaryItemShareHttpResponse(
                    s.ParticipantId.Value.ToString(),
                    s.Amount
                )).ToList()
            )).ToList()
        );

        return Results.Ok(response);
    }

    private static async Task<IResult> HandleGetParticipantSummaryAsync(
        string token,
        IBillService billService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.NotFound(new ApiError("invalid_token", "Participant access token is invalid or expired."));
        }

        var result = await billService.GetParticipantViewAsync(token, cancellationToken);
        if (result is null)
        {
            return Results.NotFound(new ApiError("access_denied", "Participant access token is invalid or expired."));
        }

        var response = new ParticipantBillViewHttpResponse(
            result.BillTitle,
            result.BillTotalAmount,
            result.ParticipantId.Value.ToString(),
            result.ParticipantPhoneNumber.Value,
            result.TotalAmountOwed,
            result.PaymentStatus.ToString(),
            result.PaidAt,
            result.Items.Select(i => new ParticipantItemShareHttpResponse(
                i.Description,
                i.Quantity,
                i.ItemTotalAmount,
                i.MyShareAmount
            )).ToList()
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
