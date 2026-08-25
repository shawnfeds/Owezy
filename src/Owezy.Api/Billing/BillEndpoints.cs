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

        app.MapGet("/participant-access/{token}", HandleGetParticipantViewAsync)
            .AllowAnonymous()
            .WithName("GetParticipantView");

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

        if (httpRequest.SharerParticipantIds is null || httpRequest.SharerParticipantIds.Count == 0)
        {
            return Results.BadRequest(new ApiError("missing_sharers", "At least one sharer participant ID is required."));
        }

        var sharerGuids = new List<ParticipantId>();
        foreach (var idStr in httpRequest.SharerParticipantIds)
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
