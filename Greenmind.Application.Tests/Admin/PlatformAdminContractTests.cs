using Greenmind.Api.Controllers.Admin;
using Greenmind.Application.Contracts.Admin;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;

namespace Greenmind.Application.Tests.Admin;

public sealed class PlatformAdminContractTests
{
    public static TheoryData<Type> AdminControllerTypes => new()
    {
        typeof(AdminFarmsController),
        typeof(AdminCropsController),
        typeof(AdminDestinationsController),
        typeof(AdminStatisticsController)
    };

    [Theory]
    [MemberData(nameof(AdminControllerTypes))]
    public void AdminControllers_RequireAdminRole(Type controllerType)
    {
        var authorizeAttribute = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(RoleNames.Admin, authorizeAttribute.Roles);
    }

    [Fact]
    public void PagedResult_CalculatesTotalPages()
    {
        var result = new PagedResult<int>([1, 2], PageNumber: 2, PageSize: 2, TotalCount: 5);

        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void OperationResult_ConflictDoesNotExposeAValue()
    {
        var result = AdminOperationResult<int>.Conflict("Duplicate master data.");

        Assert.Equal(AdminOperationStatus.Conflict, result.Status);
        Assert.Equal(default, result.Value);
        Assert.Equal("Duplicate master data.", result.ErrorMessage);
    }
}
