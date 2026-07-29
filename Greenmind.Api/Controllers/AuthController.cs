using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Greenmind.Domain.Entities;
using Greenmind.Api.Models;
using Greenmind.Application.Contracts.Persistence;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Greenmind.Application.Contracts.Api;

namespace Greenmind.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GreenmindDbContext _db;

    private const string FarmOwnerRole = "FarmOwner";
    private readonly Greenmind.Application.Services.IJwtTokenService _jwtService;

    public AuthController(UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IUnitOfWork unitOfWork,
        GreenmindDbContext db,
        Greenmind.Application.Services.IJwtTokenService jwtService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _unitOfWork = unitOfWork;
        _db = db;
        _jwtService = jwtService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return Conflict(new { Message = "Email already in use." });

        // Begin transaction to ensure user + farm created atomically
        await using var tx = await _db.Database.BeginTransactionAsync();

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            IsActive = false // new users are inactive pending admin approval
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            await _db.Database.RollbackTransactionAsync();
            return BadRequest(new { Errors = createResult.Errors.Select(e => e.Description) });
        }

        // Ensure role exists
        if (!await _roleManager.RoleExistsAsync(FarmOwnerRole))
        {
            var roleResult = await _roleManager.CreateAsync(new IdentityRole(FarmOwnerRole));
            if (!roleResult.Succeeded)
            {
                await _db.Database.RollbackTransactionAsync();
                await _userManager.DeleteAsync(user);
                return StatusCode(500, new { Message = "Failed to create role." });
            }
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, FarmOwnerRole);
        if (!addRoleResult.Succeeded)
        {
            await _db.Database.RollbackTransactionAsync();
            await _userManager.DeleteAsync(user);
            return StatusCode(500, new { Message = "Failed to assign role." });
        }

        // Create farm linked to owner
        var farm = new Farm
        {
            Name = request.FarmName,
            OwnerUserId = user.Id
        };

        await _unitOfWork.Farms.AddAsync(farm);
        await _unitOfWork.SaveChangesAsync();

        await tx.CommitAsync();

        var response = new RegisterResponse
        {
            UserId = user.Id,
            Email = user.Email!,
            FarmId = farm.Id.ToString()
        };
        return CreatedAtAction(nameof(Register), response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized();

        var valid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!valid)
            return Unauthorized();

        var farmApproval = await _db.Farms
            .AsNoTracking()
            .Where(farm => farm.OwnerUserId == user.Id)
            .Select(farm => new { farm.IsApproved })
            .SingleOrDefaultAsync();

        if (farmApproval is { IsApproved: false })
        {
            return AccountStateUnauthorized(
                ApiErrorCodes.AccountPendingApproval,
                "Account is waiting for admin approval.");
        }

        if (!user.IsActive)
        {
            return AccountStateUnauthorized(
                ApiErrorCodes.AccountInactive,
                "User account is not active.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.UserName ?? string.Empty),
            new System.Security.Claims.Claim("email", user.Email ?? string.Empty)
        };

        if (roles.Any())
        {
            foreach (var r in roles)
                claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, r));
        }

        var farmId = await _db.Farms
            .AsNoTracking()
            .Where(farm => farm.OwnerUserId == user.Id)
            .Select(farm => (int?)farm.Id)
            .SingleOrDefaultAsync();

        if (farmId.HasValue)
        {
            claims.Add(new System.Security.Claims.Claim("farmId", farmId.Value.ToString()));
        }

        var token = _jwtService.CreateToken(claims);

        return Ok(new LoginResponse { Token = token, ExpiresAtUtc = DateTime.UtcNow.AddHours(1) });
    }

    private ObjectResult AccountStateUnauthorized(string code, string message) =>
        StatusCode(
            StatusCodes.Status401Unauthorized,
            ApiResponseFactory.Failure<object?>(
                StatusCodes.Status401Unauthorized,
                code,
                message,
                HttpContext.TraceIdentifier));
}
