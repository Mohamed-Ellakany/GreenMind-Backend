using Greenmind.Application.Contracts.Admin;
using Greenmind.Application.Contracts.MasterData;
using Greenmind.Application.Dtos.Admin;
using Greenmind.Application.Dtos.MasterData;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Admin;

public sealed class PlatformAdminService(GreenmindDbContext context)
    : IPlatformAdminService, IMasterDataQueryService
{
    private const int MaximumPageSize = 100;

    public async Task<PagedResult<FarmAccountDto>> GetFarmsAsync(
        FarmAccountStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        var query = context.Farms.AsNoTracking().AsQueryable();

        query = status switch
        {
            FarmAccountStatus.Pending => query.Where(farm => !farm.IsApproved),
            FarmAccountStatus.Active => query.Where(farm => farm.IsApproved && farm.Owner.IsActive),
            FarmAccountStatus.Inactive => query.Where(farm => farm.IsApproved && !farm.Owner.IsActive),
            _ => query
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(farm => farm.CreatedAt)
            .ThenBy(farm => farm.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(farm => new FarmAccountDto(
                farm.Id,
                farm.Name,
                farm.OwnerUserId,
                farm.Owner.FullName,
                farm.Owner.Email,
                farm.IsApproved,
                farm.ApprovedAtUtc,
                farm.Owner.IsActive,
                farm.CreatedAt,
                farm.Owner.IsAiAccessSuspended))
            .ToListAsync(cancellationToken);

        return new PagedResult<FarmAccountDto>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<AdminOperationResult<FarmAccountDto>> ApproveFarmAsync(
        int farmId,
        CancellationToken cancellationToken = default)
    {
        var farm = await context.Farms
            .Include(item => item.Owner)
            .FirstOrDefaultAsync(item => item.Id == farmId, cancellationToken);

        if (farm is null)
        {
            return AdminOperationResult<FarmAccountDto>.NotFound("Farm was not found.");
        }

        if (!farm.IsApproved)
        {
            farm.IsApproved = true;
            farm.ApprovedAtUtc = DateTime.UtcNow;
        }

        // Registration keeps the owner inactive while the farm is pending.
        // Approval is the transition that grants the regular user access.
        farm.Owner.IsActive = true;
        await context.SaveChangesAsync(cancellationToken);

        return AdminOperationResult<FarmAccountDto>.Success(MapFarm(farm));
    }

    public async Task<AdminOperationResult<FarmAccountDto>> SetFarmAccountStatusAsync(
        int farmId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var farm = await context.Farms
            .Include(item => item.Owner)
            .FirstOrDefaultAsync(item => item.Id == farmId, cancellationToken);

        if (farm is null)
        {
            return AdminOperationResult<FarmAccountDto>.NotFound("Farm was not found.");
        }

        if (isActive && !farm.IsApproved)
        {
            return AdminOperationResult<FarmAccountDto>.Conflict(
                "The farm must be approved before its account can be activated.");
        }

        farm.Owner.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);

        return AdminOperationResult<FarmAccountDto>.Success(MapFarm(farm));
    }

    public async Task<IReadOnlyList<MasterDataItemDto>> GetCropsAsync(
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var query = context.Crops.AsNoTracking();

        if (!includeDeleted)
        {
            query = query.Where(crop => !crop.IsDeleted);
        }

        return await query
            .AsNoTracking()
            .OrderBy(crop => crop.Name)
            .Select(crop => new MasterDataItemDto(
                crop.Id,
                crop.Name,
                crop.Description,
                crop.IsActive,
                crop.IsDeleted,
                crop.DeletedAtUtc,
                crop.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminOperationResult<MasterDataItemDto>> CreateCropAsync(
        MasterDataRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (await context.Crops.AnyAsync(
                crop => !crop.IsDeleted && crop.Name.ToUpper() == name.ToUpper(),
                cancellationToken))
        {
            return AdminOperationResult<MasterDataItemDto>.Conflict("A crop with the same name already exists.");
        }

        var crop = new Crop { Name = name, Description = NormalizeDescription(request.Description) };
        await context.Crops.AddAsync(crop, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return AdminOperationResult<MasterDataItemDto>.Success(MapMasterData(crop));
    }

    public async Task<AdminOperationResult<MasterDataItemDto>> UpdateCropAsync(
        int id,
        MasterDataRequest request,
        CancellationToken cancellationToken = default)
    {
        var crop = await context.Crops.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (crop is null)
        {
            return AdminOperationResult<MasterDataItemDto>.NotFound("Crop was not found.");
        }

        var name = request.Name.Trim();
        if (await context.Crops.AnyAsync(
                item => item.Id != id && !item.IsDeleted && item.Name.ToUpper() == name.ToUpper(),
                cancellationToken))
        {
            return AdminOperationResult<MasterDataItemDto>.Conflict("A crop with the same name already exists.");
        }

        crop.Name = name;
        crop.Description = NormalizeDescription(request.Description);
        await context.SaveChangesAsync(cancellationToken);

        return AdminOperationResult<MasterDataItemDto>.Success(MapMasterData(crop));
    }

    public async Task<AdminOperationResult<MasterDataItemDto>> SetCropStatusAsync(
        int id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var crop = await context.Crops.FindAsync([id], cancellationToken);
        if (crop is null)
        {
            return AdminOperationResult<MasterDataItemDto>.NotFound("Crop was not found.");
        }

        if (crop.IsDeleted)
        {
            return AdminOperationResult<MasterDataItemDto>.Conflict("Restore the crop before changing its status.");
        }

        crop.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);
        return AdminOperationResult<MasterDataItemDto>.Success(MapMasterData(crop));
    }

    public async Task<AdminOperationResult<MasterDataItemDto>> DeleteCropAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var crop = await context.Crops.FindAsync([id], cancellationToken);
        if (crop is null)
        {
            return AdminOperationResult<MasterDataItemDto>.NotFound("Crop was not found.");
        }

        if (!crop.IsDeleted)
        {
            crop.IsDeleted = true;
            crop.DeletedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        return AdminOperationResult<MasterDataItemDto>.Success(MapMasterData(crop));
    }

    public async Task<AdminOperationResult<MasterDataItemDto>> RestoreCropAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var crop = await context.Crops.FindAsync([id], cancellationToken);
        if (crop is null)
        {
            return AdminOperationResult<MasterDataItemDto>.NotFound("Crop was not found.");
        }

        if (await context.Crops.AnyAsync(
                item => item.Id != id && !item.IsDeleted && item.Name.ToUpper() == crop.Name.ToUpper(),
                cancellationToken))
        {
            return AdminOperationResult<MasterDataItemDto>.Conflict("A crop with the same name already exists.");
        }

        crop.IsDeleted = false;
        crop.DeletedAtUtc = null;
        await context.SaveChangesAsync(cancellationToken);
        return AdminOperationResult<MasterDataItemDto>.Success(MapMasterData(crop));
    }

    public async Task<IReadOnlyList<MasterDataItemDto>> GetDestinationsAsync(
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var query = context.Destinations.AsNoTracking();

        if (!includeDeleted)
        {
            query = query.Where(destination => !destination.IsDeleted);
        }

        return await query
            .AsNoTracking()
            .OrderBy(destination => destination.Name)
            .Select(destination => new MasterDataItemDto(
                destination.Id,
                destination.Name,
                destination.Description,
                destination.IsActive,
                destination.IsDeleted,
                destination.DeletedAtUtc,
                destination.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminOperationResult<MasterDataItemDto>> CreateDestinationAsync(
        MasterDataRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (await context.Destinations.AnyAsync(
                destination => !destination.IsDeleted && destination.Name.ToUpper() == name.ToUpper(),
                cancellationToken))
        {
            return AdminOperationResult<MasterDataItemDto>.Conflict(
                "A destination with the same name already exists.");
        }

        var destination = new Destination
        {
            Name = name,
            Description = NormalizeDescription(request.Description)
        };

        await context.Destinations.AddAsync(destination, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return AdminOperationResult<MasterDataItemDto>.Success(MapMasterData(destination));
    }

    public async Task<AdminOperationResult<MasterDataItemDto>> UpdateDestinationAsync(
        int id,
        MasterDataRequest request,
        CancellationToken cancellationToken = default)
    {
        var destination = await context.Destinations.FirstOrDefaultAsync(
            item => item.Id == id && !item.IsDeleted,
            cancellationToken);
        if (destination is null)
        {
            return AdminOperationResult<MasterDataItemDto>.NotFound("Destination was not found.");
        }

        var name = request.Name.Trim();
        if (await context.Destinations.AnyAsync(
                item => item.Id != id && !item.IsDeleted && item.Name.ToUpper() == name.ToUpper(),
                cancellationToken))
        {
            return AdminOperationResult<MasterDataItemDto>.Conflict(
                "A destination with the same name already exists.");
        }

        destination.Name = name;
        destination.Description = NormalizeDescription(request.Description);
        await context.SaveChangesAsync(cancellationToken);

        return AdminOperationResult<MasterDataItemDto>.Success(MapMasterData(destination));
    }

    public async Task<AdminOperationResult<MasterDataItemDto>> SetDestinationStatusAsync(
        int id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var destination = await context.Destinations.FindAsync([id], cancellationToken);
        if (destination is null)
        {
            return AdminOperationResult<MasterDataItemDto>.NotFound("Destination was not found.");
        }

        if (destination.IsDeleted)
        {
            return AdminOperationResult<MasterDataItemDto>.Conflict("Restore the destination before changing its status.");
        }

        destination.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);
        return AdminOperationResult<MasterDataItemDto>.Success(MapMasterData(destination));
    }

    public async Task<AdminOperationResult<MasterDataItemDto>> DeleteDestinationAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var destination = await context.Destinations.FindAsync([id], cancellationToken);
        if (destination is null)
        {
            return AdminOperationResult<MasterDataItemDto>.NotFound("Destination was not found.");
        }

        if (!destination.IsDeleted)
        {
            destination.IsDeleted = true;
            destination.DeletedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        return AdminOperationResult<MasterDataItemDto>.Success(MapMasterData(destination));
    }

    public async Task<AdminOperationResult<MasterDataItemDto>> RestoreDestinationAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var destination = await context.Destinations.FindAsync([id], cancellationToken);
        if (destination is null)
        {
            return AdminOperationResult<MasterDataItemDto>.NotFound("Destination was not found.");
        }

        if (await context.Destinations.AnyAsync(
                item => item.Id != id
                    && !item.IsDeleted
                    && item.Name.ToUpper() == destination.Name.ToUpper(),
                cancellationToken))
        {
            return AdminOperationResult<MasterDataItemDto>.Conflict(
                "A destination with the same name already exists.");
        }

        destination.IsDeleted = false;
        destination.DeletedAtUtc = null;
        await context.SaveChangesAsync(cancellationToken);
        return AdminOperationResult<MasterDataItemDto>.Success(MapMasterData(destination));
    }

    public async Task<IReadOnlyList<MasterDataItemDto>> GetActiveCropsAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.Crops
            .AsNoTracking()
            .Where(crop => crop.IsActive && !crop.IsDeleted)
            .OrderBy(crop => crop.Name)
            .Select(crop => new MasterDataItemDto(
                crop.Id,
                crop.Name,
                crop.Description,
                crop.IsActive,
                crop.IsDeleted,
                crop.DeletedAtUtc,
                crop.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MasterDataItemDto>> GetActiveDestinationsAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.Destinations
            .AsNoTracking()
            .Where(destination => destination.IsActive && !destination.IsDeleted)
            .OrderBy(destination => destination.Name)
            .Select(destination => new MasterDataItemDto(
                destination.Id,
                destination.Name,
                destination.Description,
                destination.IsActive,
                destination.IsDeleted,
                destination.DeletedAtUtc,
                destination.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private static FarmAccountDto MapFarm(Farm farm) =>
        new(
            farm.Id,
            farm.Name,
            farm.OwnerUserId,
            farm.Owner.FullName,
            farm.Owner.Email,
            farm.IsApproved,
            farm.ApprovedAtUtc,
            farm.Owner.IsActive,
            farm.CreatedAt,
            farm.Owner.IsAiAccessSuspended);

    private static MasterDataItemDto MapMasterData(Crop crop) =>
        new(
            crop.Id,
            crop.Name,
            crop.Description,
            crop.IsActive,
            crop.IsDeleted,
            crop.DeletedAtUtc,
            crop.CreatedAt);

    private static MasterDataItemDto MapMasterData(Destination destination) =>
        new(
            destination.Id,
            destination.Name,
            destination.Description,
            destination.IsActive,
            destination.IsDeleted,
            destination.DeletedAtUtc,
            destination.CreatedAt);

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
