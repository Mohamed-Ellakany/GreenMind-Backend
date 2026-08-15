using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Greenmind.Domain.Entities;
using Greenmind.Api.Models;
using Greenmind.Application.Contracts.Persistence;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using Greenmind.Api.Services;
using Greenmind.Application.Contracts.Api;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Greenmind.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GreenmindDbContext _db;
    private readonly IAccountEmailService _emailService;
    private readonly ILogger<AuthController> _logger;

    private const string FarmOwnerRole = "FarmOwner";
    private readonly Greenmind.Application.Services.IJwtTokenService _jwtService;

    public AuthController(UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IUnitOfWork unitOfWork,
        GreenmindDbContext db,
        Greenmind.Application.Services.IJwtTokenService jwtService,
        IAccountEmailService emailService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _unitOfWork = unitOfWork;
        _db = db;
        _jwtService = jwtService;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return Conflict(new { Message = "Email already in use." });

        var strategy = _db.Database.CreateExecutionStrategy();

        ApplicationUser? createdUser = null;
        Farm? createdFarm = null;
        string? activationToken = null;

        var executionResult = await strategy.ExecuteAsync<IActionResult?>(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                IsActive = false // Inactive pending email activation
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                await tx.RollbackAsync();
                return BadRequest(new { Errors = createResult.Errors.Select(e => e.Description) });
            }

            // Ensure role exists
            if (!await _roleManager.RoleExistsAsync(FarmOwnerRole))
            {
                var roleResult = await _roleManager.CreateAsync(new IdentityRole(FarmOwnerRole));
                if (!roleResult.Succeeded)
                {
                    await tx.RollbackAsync();
                    return StatusCode(500, new { Message = "Failed to create role." });
                }
            }

            var addRoleResult = await _userManager.AddToRoleAsync(user, FarmOwnerRole);
            if (!addRoleResult.Succeeded)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { Message = "Failed to assign role." });
            }

            // Create farm linked to owner - auto-approved (no admin approval required)
            var farm = new Farm
            {
                Name = request.FarmName,
                OwnerUserId = user.Id,
                IsApproved = true,
                ApprovedAtUtc = DateTime.UtcNow
            };

            await _unitOfWork.Farms.AddAsync(farm);
            await _unitOfWork.SaveChangesAsync();

            _db.ExpenseCategories.AddRange(DefaultExpenseCategories.ArabicNames.Select(name =>
                new ExpenseCategory
                {
                    FarmId = farm.Id,
                    Name = name,
                    IsDefault = true
                }));
            await _unitOfWork.SaveChangesAsync();

            await tx.CommitAsync();

            createdUser = user;
            createdFarm = farm;

            return null;
        });

        if (executionResult is not null)
        {
            return executionResult;
        }

        if (createdUser is not null && createdFarm is not null)
        {
            activationToken = await _userManager.GenerateEmailConfirmationTokenAsync(createdUser);
            await TrySendActivationEmailAsync(createdUser, activationToken);

            var response = new RegisterResponse
            {
                UserId = createdUser.Id,
                Email = createdUser.Email!,
                FarmId = createdFarm.Id.ToString()
            };
            return CreatedAtAction(nameof(Register), response);
        }

        return StatusCode(500, new { Message = "Registration failed unexpectedly." });
    }

    [AllowAnonymous]
    [HttpPost("activate-account")]
    public async Task<IActionResult> ActivateAccount([FromBody] ActivateAccountRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return BadRequest(new { Message = "Invalid activation request." });

        if (user.IsActive && user.EmailConfirmed)
        {
            return Ok(new { Message = "Account is already activated. You can proceed to log in." });
        }

        var result = await _userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            return BadRequest(new { Message = "Activation token is invalid or has expired.", Errors = result.Errors.Select(e => e.Description) });
        }

        user.IsActive = true;
        await _userManager.UpdateAsync(user);

        var farm = await _db.Farms.SingleOrDefaultAsync(f => f.OwnerUserId == user.Id, cancellationToken);
        if (farm is not null && !farm.IsApproved)
        {
            farm.IsApproved = true;
            farm.ApprovedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { Message = "Account activated successfully. You can now log in." });
    }

    [AllowAnonymous]
    [HttpPost("resend-activation")]
    public async Task<IActionResult> ResendActivation([FromBody] ResendActivationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null && (!user.IsActive || !user.EmailConfirmed) && _emailService.IsConfigured)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await _emailService.SendAccountActivationAsync(user.Email!, user.FullName, token, cancellationToken);
        }

        return Accepted(new { Message = "If an inactive account with this email exists, an activation link has been sent." });
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

        if (!user.IsActive || !user.EmailConfirmed)
        {
            return AccountStateUnauthorized(
                ApiErrorCodes.AccountInactive,
                "Account is not activated yet. Please activate your account using the confirmation link sent to your email.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.UserName ?? string.Empty),
            new System.Security.Claims.Claim("fullName", user.FullName),
            new System.Security.Claims.Claim("email", user.Email ?? string.Empty)
        };

        if (roles.Any())
        {
            foreach (var r in roles)
                claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, r));
        }

        var farmInfo = await _db.Farms
            .AsNoTracking()
            .Where(farm => farm.OwnerUserId == user.Id)
            .Select(farm => new { farm.Id, farm.Name })
            .SingleOrDefaultAsync();

        if (farmInfo is not null)
        {
            claims.Add(new System.Security.Claims.Claim("farmId", farmInfo.Id.ToString()));
            claims.Add(new System.Security.Claims.Claim("farmName", farmInfo.Name));
        }

        var token = _jwtService.CreateToken(claims);

        return Ok(new LoginResponse { Token = token, ExpiresAtUtc = DateTime.UtcNow.AddHours(1) });
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null && !string.IsNullOrWhiteSpace(user.Email) && _emailService.IsConfigured)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _emailService.SendPasswordResetAsync(user.Email, user.FullName, token, cancellationToken);
        }
        return Accepted(new { Message = "If the email exists, a reset link has been sent." });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null) return BadRequest(new { Message = "Invalid reset request." });
        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        return result.Succeeded ? NoContent() : BadRequest(new { Errors = result.Errors.Select(error => error.Description) });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var account = await _db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new AccountProfileResponse(
                user.FullName,
                user.Email,
                user.Farm == null ? null : user.Farm.Name,
                user.Farm == null ? null : user.Farm.Id,
                user.IsActive,
                user.Farm == null ? null : user.Farm.IsApproved))
            .SingleOrDefaultAsync(cancellationToken);

        return account is null ? Unauthorized() : Ok(account);
    }

    [Authorize]
    [HttpPut("me/profile")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateAccountProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized();

        var email = request.Email.Trim();
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null && existing.Id != user.Id)
            return Conflict(new { Message = "Email already in use." });

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.UserName = email;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { Errors = result.Errors.Select(error => error.Description) });

        return await Me(cancellationToken);
    }

    [Authorize(Roles = RoleNames.FarmOwner)]
    [HttpPut("me/farm")]
    public async Task<IActionResult> UpdateMyFarm([FromBody] UpdateMyFarmRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var farm = await _db.Farms.SingleOrDefaultAsync(item => item.OwnerUserId == userId, cancellationToken);
        if (farm is null)
            return NotFound();

        var farmName = request.FarmName.Trim();
        if (farmName.Length == 0)
            return BadRequest(new { Message = "Farm name is required." });

        farm.Name = farmName;
        await _db.SaveChangesAsync(cancellationToken);

        return await Me(cancellationToken);
    }

    [Authorize]
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangeMyPassword([FromBody] ChangeAccountPasswordRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { Errors = result.Errors.Select(error => error.Description) });

        return NoContent();
    }

    private ObjectResult AccountStateUnauthorized(string code, string message) =>
        StatusCode(
            StatusCodes.Status401Unauthorized,
            ApiResponseFactory.Failure<object?>(
                StatusCodes.Status401Unauthorized,
                code,
                message,
                HttpContext.TraceIdentifier));

    private async Task TrySendActivationEmailAsync(ApplicationUser user, string token)
    {
        if (!_emailService.IsConfigured)
        {
            _logger.LogInformation(
                "Skipping activation email for user {UserId} because SMTP is not configured.",
                user.Id);
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                await _emailService.SendAccountActivationAsync(
                    user.Email,
                    user.FullName,
                    token,
                    HttpContext.RequestAborted);
            }
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            _logger.LogWarning(
                exception,
                "Activation email could not be sent for user {UserId}.",
                user.Id);
        }
    }
}

public sealed record AccountProfileResponse(
    string FullName,
    string? Email,
    string? FarmName,
    int? FarmId,
    bool IsActive,
    bool? IsFarmApproved);

public sealed record UpdateAccountProfileRequest(
    [Required, MaxLength(150)] string FullName,
    [Required, EmailAddress, MaxLength(256)] string Email);

public sealed record UpdateMyFarmRequest(
    [Required, MaxLength(200)] string FarmName);

public sealed record ChangeAccountPasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);

public sealed record ForgotPasswordRequest([Required, EmailAddress] string Email);
public sealed record ResetPasswordRequest([Required, EmailAddress] string Email, [Required] string Token, [Required, MinLength(8)] string NewPassword);
public sealed record ActivateAccountRequest([Required, EmailAddress] string Email, [Required] string Token);
public sealed record ResendActivationRequest([Required, EmailAddress] string Email);
