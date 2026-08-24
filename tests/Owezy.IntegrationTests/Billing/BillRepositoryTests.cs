using Microsoft.EntityFrameworkCore;
using Owezy.Domain.Auth;
using Owezy.Domain.Billing;
using Owezy.Infrastructure.Persistence;
using Xunit;

namespace Owezy.IntegrationTests.Billing;

public sealed class BillRepositoryTests : IAsyncLifetime
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

    [Fact(DisplayName = "Create + Retrieve: Bill and initial participant round-trips correctly")]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsCorrectBillAndParticipants()
    {
        SkipIfUnavailable();

        var splitterPhone = PhoneNumber.Create("+919876543210");
        var now = DateTimeOffset.UtcNow;
        var bill = Bill.Create("Team Lunch", splitterPhone, now);

        await _repository.AddAsync(bill);

        var retrieved = await _repository.GetByIdAsync(bill.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(bill.Id, retrieved.Id);
        Assert.Equal("Team Lunch", retrieved.Title);
        Assert.Equal(splitterPhone, retrieved.SplitterPhoneNumber);
        Assert.Single(retrieved.Participants);
        Assert.Equal(splitterPhone, retrieved.Participants.First().PhoneNumber);
    }

    [Fact(DisplayName = "Add Participant: updates relationship correctly in database")]
    public async Task UpdateAsync_AddedParticipant_IsPersisted()
    {
        SkipIfUnavailable();

        var splitterPhone = PhoneNumber.Create("+919876543210");
        var participantPhone = PhoneNumber.Create("+919123456789");
        var now = DateTimeOffset.UtcNow;
        var bill = Bill.Create("Party Expense", splitterPhone, now);

        await _repository.AddAsync(bill);

        bill.AddParticipant(participantPhone, now.AddMinutes(5));
        await _repository.UpdateAsync(bill);

        // Re-fetch with fresh DbContext
        await _context.DisposeAsync();
        var freshOptions = new DbContextOptionsBuilder<OwezyDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        _context = new OwezyDbContext(freshOptions);
        _repository = new SqlBillRepository(_context);

        var refreshed = await _repository.GetByIdAsync(bill.Id);

        Assert.NotNull(refreshed);
        Assert.Equal(2, refreshed.Participants.Count);
        Assert.Contains(refreshed.Participants, p => p.PhoneNumber == participantPhone);
    }

    [Fact(DisplayName = "Duplicate Participant: DB unique index prevents duplicate membership")]
    public async Task DbConstraint_DuplicateParticipant_ThrowsDbUpdateException()
    {
        SkipIfUnavailable();

        var splitterPhone = PhoneNumber.Create("+919876543210");
        var now = DateTimeOffset.UtcNow;
        var bill = Bill.Create("Trip", splitterPhone, now);

        await _repository.AddAsync(bill);

        // Directly insert duplicate row into DbContext to test DB unique index (BillId, PhoneNumber)
        _context.BillParticipants.Add(new BillParticipantRow
        {
            Id = Guid.NewGuid(),
            BillId = bill.Id.Value,
            PhoneNumber = splitterPhone.Value,
            JoinedAt = now
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }
}
