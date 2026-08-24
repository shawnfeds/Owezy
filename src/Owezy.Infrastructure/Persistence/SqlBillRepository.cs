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
            .Include(b => b.Items)
                .ThenInclude(i => i.Sharers)
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
            .Include(b => b.Items)
                .ThenInclude(i => i.Sharers)
            .FirstOrDefaultAsync(b => b.Id == bill.Id.Value, cancellationToken);

        if (existingRow is null)
        {
            throw new InvalidOperationException($"Bill with ID '{bill.Id}' was not found for update.");
        }

        existingRow.Title = bill.Title;
        existingRow.Status = (int)bill.Status;
        existingRow.FinalizedAt = bill.FinalizedAt;

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

        // Sync items
        foreach (var item in bill.Items)
        {
            var existingItem = existingRow.Items.FirstOrDefault(i => i.Id == item.Id.Value);
            if (existingItem is null)
            {
                var newItemRow = new BillItemRow
                {
                    Id = item.Id.Value,
                    BillId = bill.Id.Value,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    Amount = item.Amount,
                    Sharers = item.SharerParticipantIds.Select(s => new BillItemSharerRow
                    {
                        ItemId = item.Id.Value,
                        ParticipantId = s.Value
                    }).ToList()
                };
                existingRow.Items.Add(newItemRow);
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
            FinalizedAt = bill.FinalizedAt,
            Participants = bill.Participants.Select(p => new BillParticipantRow
            {
                Id = p.Id.Value,
                BillId = bill.Id.Value,
                PhoneNumber = p.PhoneNumber.Value,
                JoinedAt = p.JoinedAt
            }).ToList(),
            Items = bill.Items.Select(i => new BillItemRow
            {
                Id = i.Id.Value,
                BillId = bill.Id.Value,
                Description = i.Description,
                Quantity = i.Quantity,
                Amount = i.Amount,
                Sharers = i.SharerParticipantIds.Select(s => new BillItemSharerRow
                {
                    ItemId = i.Id.Value,
                    ParticipantId = s.Value
                }).ToList()
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

        var items = row.Items.Select(i => BillItem.Reconstitute(
            new BillItemId(i.Id),
            new BillId(i.BillId),
            i.Description,
            i.Quantity,
            i.Amount,
            i.Sharers.Select(s => new ParticipantId(s.ParticipantId))
        ));

        return Bill.Reconstitute(
            new BillId(row.Id),
            row.Title,
            PhoneNumber.Create(row.SplitterPhoneNumber),
            row.CreatedAt,
            (BillStatus)row.Status,
            participants,
            items,
            row.FinalizedAt
        );
    }
}
