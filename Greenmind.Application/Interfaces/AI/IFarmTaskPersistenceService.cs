using Greenmind.Application.Dtos.AI;
using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Interfaces.AI
{
    public interface IFarmTaskPersistenceService
    {
        Task SaveGeneratedTasksAsync(int exportPlanId, AgentResponse agentResponse);
    }
}
