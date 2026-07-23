using Greenmind.Application.Interfaces;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Greenmind.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<GreenmindDbContext>(options =>
            {
                if (string.Equals(configuration["UseInMemoryDatabase"], "true", StringComparison.OrdinalIgnoreCase))
                {
                    var databaseName = configuration["InMemoryDatabaseName"] ?? "GreenmindTest";
                    options.UseInMemoryDatabase(databaseName);
                }
                else
                {
                    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
                }
            });

            services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<GreenmindDbContext>()
                .AddDefaultTokenProviders();

            services.AddScoped<IFarmRepository, FarmRepository>();

            return services;
        }
    }
}
