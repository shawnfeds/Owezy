using Microsoft.EntityFrameworkCore;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Owezy.Infrastructure.Persistence;
using Xunit;

namespace Owezy.IntegrationTests.Billing;

public sealed class BillItemRepositoryTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=Owezy_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=true;";

    private OwezyDbContext _context = null!;
    private SqlBillRepository _repository = null!;
    private bool _sqlServerAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            var options = new DbContextOptionsBuilder<OwezyDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

            _context = new OwezyDbContext(options);
            await _context.Database.MigrateAsync();
            _sqlServerAvailable = true;
        }
        catch (Exception)
        {
            _sqlServerAvailable = false;
        }

        if (_sqlServerAvailable)
        {
            _repository = new SqlBillRepository(_context);
        }
    }

    public async Task DisposeAsync()
    {
        if (_sqlServerAvailable && _context is not null)
        {
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM BillItemSharers");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM BillItems");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM BillParticipants");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Bills");
            await _context.DisposeAsync();
        }
    }

    private void SkipIfUnavailable()
    {
        if (!_sqlServerAvailable)
        {
            throw new SkipException("SQL Server (LocalDB) is not available in this environment.");
        }
    }

    [Fact(DisplayName = "Bill Item Persistence: Items and Sharers persist and round-trip correctly")]
    public async Task UpdateAsync_BillWithItemsAndSharers_PersistsCorrectly()
    {
        SkipIfUnavailable();

        var splitterPhone = PhoneNumber.Create("+919876543210");
        var participantPhone = PhoneNumber.Create("+919123456789");
        var now = DateTimeOffset.UtcNow;
        var bill = Bill.Create("Pizza Party", splitterPhone, now);
        var part = bill.AddParticipant(participantPhone, now.AddMinutes(2));
        var splitterPart = bill.Participants.First(p => p.PhoneNumber == splitterPhone);

        await _repository.AddAsync(bill);

        // Add item to bill
        var item = bill.AddItem("Extra Cheese Pizza", 3, 1200.00m, new[] { splitterPart.Id, part.Id });
        await _repository.UpdateAsync(bill);

        // Re-fetch from fresh DbContext
        await _context.DisposeAsync();
        var freshOptions = new DbContextOptionsBuilder<OwezyDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        _context = new OwezyDbContext(freshOptions);
        _repository = new SqlBillRepository(_context);

        var refreshed = await _repository.GetByIdAsync(bill.Id);

        Assert.NotNull(refreshed);
        Assert.Single(refreshed.Items);

        var retrievedItem = refreshed.Items.First();
        Assert.Equal(item.Id, retrievedItem.Id);
        Assert.Equal("Extra Cheese Pizza", retrievedItem.Description);
        Assert.Equal(3, retrievedItem.Quantity);
        Assert.Equal(1200.00m, retrievedItem.Amount);
        Assert.Equal(2, retrievedItem.SharerParticipantIds.Count);
        Assert.Contains(splitterPart.Id, retrievedItem.SharerParticipantIds);
        Assert.Contains(part.Id, retrievedItem.SharerParticipantIds);
    }
}
