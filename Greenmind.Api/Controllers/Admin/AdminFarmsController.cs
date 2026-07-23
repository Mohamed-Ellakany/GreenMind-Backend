using Greenmind.Application.Exceptions;
using Greenmind.Application.Interfaces;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/farms")]
    [Authorize(Roles = AppRoles.PlatformAdmin)]
    public class AdminFarmsController(IAdminFarmService adminFarmService) : ControllerBase
    {
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingRegistrations(CancellationToken cancellationToken)
        {
            var farms = await adminFarmService.GetPendingRegistrationsAsync(cancellationToken);
            return Ok(farms);
        }

        [HttpPost("{farmId:int}/approve")]
        public async Task<IActionResult> ApproveFarm(int farmId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await adminFarmService.ApproveFarmAsync(farmId, cancellationToken);
                return Ok(result);
            }
            catch (BusinessRuleException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("{farmId:int}/activate")]
        public async Task<IActionResult> ActivateFarm(int farmId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await adminFarmService.ActivateFarmAsync(farmId, cancellationToken);
                return Ok(result);
            }
            catch (BusinessRuleException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("{farmId:int}/deactivate")]
        public async Task<IActionResult> DeactivateFarm(int farmId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await adminFarmService.DeactivateFarmAsync(farmId, cancellationToken);
                return Ok(result);
            }
            catch (BusinessRuleException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }
    }
}
