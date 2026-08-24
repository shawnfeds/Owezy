using Microsoft.EntityFrameworkCore;
using Owezy.Application.Billing;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;

namespace Owezy.Infrastructure.Persistence;

public sealed class SqlBillRepository : IBillRepository
{
    private readonly OwezyDbContext _context;

    public SqlBillRepository(OwezyDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Bill?> GetByIdAsync(BillId id, CancellationToken cancellationToken = default)
    {
        var row = await _context.Bills
            .Include(b => b.Participants)
            .FirstOrDefaultAsync(b => b.Id == id.Value, cancellationToken);

        if (row is null)
        {
            return null;
        }

        return MapToDomain(row);
    }

    public async Task AddAsync(Bill bill, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bill);

        var row = MapToRow(bill);
        await _context.Bills.AddAsync(row, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Bill bill, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bill);

        var existingRow = await _context.Bills
            .Include(b => b.Participants)
            .FirstOrDefaultAsync(b => b.Id == bill.Id.Value, cancellationToken);

        if (existingRow is null)
        {
            throw new InvalidOperationException($"Bill with ID '{bill.Id}' was not found for update.");
        }

        existingRow.Title = bill.Title;
        existingRow.Status = (int)bill.Status;

        // Sync participants
        foreach (var participant in bill.Participants)
        {
            if (!existingRow.Participants.Any(p => p.Id == participant.Id.Value))
            {
                existingRow.Participants.Add(new BillParticipantRow
                {
                    Id = participant.Id.Value,
                    BillId = bill.Id.Value,
                    PhoneNumber = participant.PhoneNumber.Value,
                    JoinedAt = participant.JoinedAt
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static BillRow MapToRow(Bill bill)
    {
        return new BillRow
        {
            Id = bill.Id.Value,
            Title = bill.Title,
            SplitterPhoneNumber = bill.SplitterPhoneNumber.Value,
            Status = (int)bill.Status,
            CreatedAt = bill.CreatedAt,
            Participants = bill.Participants.Select(p => new BillParticipantRow
            {
                Id = p.Id.Value,
                BillId = bill.Id.Value,
                PhoneNumber = p.PhoneNumber.Value,
                JoinedAt = p.JoinedAt
            }).ToList()
        };
    }

    private static Bill MapToDomain(BillRow row)
    {
        var participants = row.Participants.Select(p => Participant.Reconstitute(
            new ParticipantId(p.Id),
            new BillId(p.BillId),
            PhoneNumber.Create(p.PhoneNumber),
            p.JoinedAt
        ));

        return Bill.Reconstitute(
            new BillId(row.Id),
            row.Title,
            PhoneNumber.Create(row.SplitterPhoneNumber),
            row.CreatedAt,
            (BillStatus)row.Status,
            participants
        );
    }
}
