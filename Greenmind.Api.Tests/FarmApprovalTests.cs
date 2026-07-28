using Greenmind.Application.Contracts.Admin;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Admin;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Greenmind.Api.Tests;

public sealed class FarmApprovalTests
{
    [Fact]
    public async Task ApproveFarm_ActivatesRegisteredOwner()
    {
        var options = new DbContextOptionsBuilder<GreenmindDbContext>()
            .UseInMemoryDatabase($"FarmApproval-{Guid.NewGuid()}")
            .Options;

        await using var context = new GreenmindDbContext(options);
        var owner = new ApplicationUser
        {
            Id = "pending-owner",
            UserName = "owner@test.local",
            Email = "owner@test.local",
            FullName = "Pending Owner",
            IsActive = false
        };
        var farm = new Farm
        {
            Id = 42,
            Name = "Pending Farm",
            OwnerUserId = owner.Id,
            Owner = owner,
            IsApproved = false
        };

        context.Users.Add(owner);
        context.Farms.Add(farm);
        await context.SaveChangesAsync();

        var service = new PlatformAdminService(context);
        var result = await service.ApproveFarmAsync(farm.Id);

        Assert.Equal(AdminOperationStatus.Succeeded, result.Status);
        Assert.True(result.Value!.IsApproved);
        Assert.True(result.Value.IsActive);
        Assert.True((await context.Users.SingleAsync(user => user.Id == owner.Id)).IsActive);
    }
}
