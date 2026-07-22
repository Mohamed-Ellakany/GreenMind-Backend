using Microsoft.EntityFrameworkCore;
using Greenmind.Infrastructure;
using Greenmind.Api.Filters;
using Greenmind.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Greenmind.Infrastructure.Agents;

namespace Greenmind.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ModelStateValidationFilter>();
                options.Filters.Add<ApiResponseWrappingFilter>();
            });

            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });

            builder.Services.AddInfrastructureServices();

            builder.Services.AddDbContext<GreenmindDbContext>(options =>
                 options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<GreenmindDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

            builder.Services.AddAuthorization();

            builder.Services.AddFarmPlanningKernel(builder.Configuration);

            builder.Services.AddSwaggerGen();

            var app = builder.Build();

			// Configure the HTTP request pipeline.

            app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            app.UseAuthentication();

            app.UseMiddleware<FarmAccountAccessMiddleware>();

            app.UseMiddleware<AuthorizationFailureLoggingMiddleware>();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
