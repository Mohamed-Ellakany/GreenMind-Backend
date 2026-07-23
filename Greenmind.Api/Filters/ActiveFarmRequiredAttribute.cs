using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ActiveFarmRequiredAttribute : TypeFilterAttribute
    {
        public ActiveFarmRequiredAttribute() : base(typeof(ActiveFarmRequiredFilter))
        {
        }
    }
}
