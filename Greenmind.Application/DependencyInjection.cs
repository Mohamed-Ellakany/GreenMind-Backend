using Greenmind.Application.Interfaces;
using Greenmind.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IAdminFarmService, AdminFarmService>();
            services.AddScoped<ISeasonCalculationService, SeasonCalculationService>();

            return services;
        }
    }
}
