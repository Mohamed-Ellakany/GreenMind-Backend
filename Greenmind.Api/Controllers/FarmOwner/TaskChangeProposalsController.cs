using System.Security.Claims;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.TaskProposals;
using Greenmind.Application.Dtos.TaskProposals;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/task-change-proposals")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class TaskChangeProposalsController(
    ITaskChangeProposalService proposalService) : ControllerBase
{
    [HttpGet("{proposalId:int}")]
    public async Task<ActionResult<TaskChangeProposalDto>> Get(
        int proposalId,
        CancellationToken cancellationToken)
    {
        var result = await proposalService.GetAsync(
            proposalId,
            GetOwnerUserId(),
            cancellationToken);
        return Map(result);
    }

    [HttpPost("{proposalId:int}/apply")]
    public async Task<ActionResult<TaskChangeProposalDto>> Apply(
        int proposalId,
        CancellationToken cancellationToken)
    {
        var result = await proposalService.ApplyAsync(
            proposalId,
            GetOwnerUserId(),
            cancellationToken);
        return Map(result);
    }

    [HttpPost("{proposalId:int}/reject")]
    public async Task<ActionResult<TaskChangeProposalDto>> Reject(
        int proposalId,
        CancellationToken cancellationToken)
    {
        var result = await proposalService.RejectAsync(
            proposalId,
            GetOwnerUserId(),
            cancellationToken);
        return Map(result);
    }

    [HttpPut("{proposalId:int}")]
    public async Task<ActionResult<TaskChangeProposalDto>> Update(
        int proposalId,
        [FromBody] UpdateTaskProposalRequest request,
        CancellationToken cancellationToken)
    {
        var result = await proposalService.UpdateAsync(
            proposalId,
            GetOwnerUserId(),
            request,
            cancellationToken);
        return Map(result);
    }

    private ActionResult<TaskChangeProposalDto> Map(
        TaskProposalResult<TaskChangeProposalDto> result) =>
        result.Status switch
        {
            TaskProposalResultStatus.Succeeded => Ok(result.Value),
            TaskProposalResultStatus.NotFound => NotFound(),
            _ => Conflict(ApiResponseFactory.Failure<TaskChangeProposalDto>(
                StatusCodes.Status409Conflict,
                result.ErrorCode ?? ApiErrorCodes.Conflict,
                result.ErrorMessage ?? "تعذر تنفيذ الاقتراح.",
                HttpContext.TraceIdentifier))
        };

    private string GetOwnerUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("An authenticated user id is required.");
}
