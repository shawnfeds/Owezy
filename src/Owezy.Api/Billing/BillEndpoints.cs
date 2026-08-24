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
