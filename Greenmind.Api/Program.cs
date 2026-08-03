using Microsoft.EntityFrameworkCore;
using Greenmind.Infrastructure;
using Greenmind.Api.Filters;
using Greenmind.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Greenmind.Api.Seed;
using Greenmind.Infrastructure.Agents;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Greenmind.Infrastructure.Storage;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Serilog;
using Serilog.Sinks.MSSqlServer;
using Greenmind.Api.Services;
// OpenAPI models namespace removed; not used when Swagger security is not configured here

namespace Greenmind.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
            });


            // Add services to the container.
            builder.Services.AddScoped<IAccountEmailService, AccountEmailService>();

            // Files under this root are private application data. The app intentionally
            // does not enable static-file middleware; authorized controllers stream them.
            builder.Services.Configure<PrivateFileStorageOptions>(options =>
                options.RootPath = Path.Combine(
                    builder.Environment.ContentRootPath,
                    "wwwroot",
                    "private"));

            builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ModelStateValidationFilter>();
                options.Filters.Add<ApiResponseWrappingFilter>();
            });

            // DateOnly JSON converter
            builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(opts =>
            {
                opts.JsonSerializerOptions.Converters.Add(new Greenmind.Api.Json.DateOnlyConverter());
                opts.JsonSerializerOptions.Converters.Add(new Greenmind.Api.Json.ArabicBusinessEnumJsonConverterFactory());
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });

            // Database provider selection: by default use SQL Server in production.
            var useInMemory = false;
            if (!string.IsNullOrEmpty(builder.Configuration["UseInMemoryDatabase"]) && bool.TryParse(builder.Configuration["UseInMemoryDatabase"], out var inMem))
            {
                useInMemory = inMem;
            }

            var onlineConnectionString = builder.Configuration.GetConnectionString("online");
            var databaseConnectionString = string.IsNullOrWhiteSpace(onlineConnectionString)
                ? builder.Configuration.GetConnectionString("DefaultConnection")
                : onlineConnectionString;

            builder.Services.AddSerilog((services, loggerConfiguration) =>
            {
                loggerConfiguration
                    .ReadFrom.Configuration(builder.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console();

                if (!useInMemory &&
                    !builder.Environment.IsEnvironment("Testing") &&
                    !string.IsNullOrWhiteSpace(databaseConnectionString))
                {
                    var columnOptions = new ColumnOptions();
                    columnOptions.Store.Remove(StandardColumn.Properties);
                    columnOptions.Store.Add(StandardColumn.LogEvent);
                    columnOptions.TimeStamp.ConvertToUtc = true;

                    loggerConfiguration.WriteTo.MSSqlServer(
                        connectionString: databaseConnectionString,
                        sinkOptions: new MSSqlServerSinkOptions
                        {
                            TableName = "Logs",
                            AutoCreateSqlTable = false
                        },
                        columnOptions: columnOptions);
                }
            });

            if (useInMemory)
            {
                // Use InMemory provider for tests or local scenarios
                builder.Services.AddDbContext<GreenmindDbContext>(options =>
                    options.UseInMemoryDatabase(builder.Configuration["InMemoryDatabaseName"] ?? "GreenmindTestDb"));
                builder.Services.AddInfrastructureServices();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(databaseConnectionString))
                {
                    throw new InvalidOperationException(
                        "Configure ConnectionStrings:online or ConnectionStrings:DefaultConnection.");
                }

                builder.Services.AddDbContext<GreenmindDbContext>(options =>
                    options.UseSqlServer(databaseConnectionString));

                builder.Services.AddInfrastructureServices();
            }

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
            
            // JWT Authentication
            var jwtKey = builder.Configuration["Jwt:Key"] ?? "greenmind_development_only_key_32_bytes";
            var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };
            });


            
          

            // Simple role-based authorization
            builder.Services.AddAuthorization();

            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("ai-chat", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? context.Connection.RemoteIpAddress?.ToString()
                            ?? "anonymous",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 60,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
                options.AddPolicy("ai-analysis", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? context.Connection.RemoteIpAddress?.ToString()
                            ?? "anonymous",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
                options.AddPolicy("private-files", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? context.Connection.RemoteIpAddress?.ToString()
                            ?? "anonymous",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 120,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });

            builder.Services.AddFarmPlanningKernel(builder.Configuration);

            builder.Services.AddSwaggerGen(options =>
            {
                options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);
            });

            var planningEnabled = builder.Configuration.GetValue("Planning:Enabled", true);
            if (planningEnabled && !builder.Environment.IsEnvironment("Testing"))
            {
                builder.Services.AddHostedService<Greenmind.Api.HostedServices.InitialExportPlanPlanningService>();
                builder.Services.AddHostedService<Greenmind.Api.HostedServices.DailyRollingPlanningService>();
            }

            if (!builder.Environment.IsEnvironment("Testing"))
            {
                builder.Services.AddHostedService<Greenmind.Api.HostedServices.DiseaseAnalysisBackgroundService>();
            }

            var app = builder.Build();

            app.UseCors("AllowAll");

            // Ensure roles and an admin user exist on startup unless explicitly skipped (useful for tests)
            var skipSeeding = false;
            if (!string.IsNullOrEmpty(app.Configuration["SkipSeeding"]) && bool.TryParse(app.Configuration["SkipSeeding"], out var parsed))
            {
                skipSeeding = parsed;
            }

            if (!skipSeeding)
            {
                app.EnsureRolesAndAdminAsync().GetAwaiter().GetResult();
            }

			// Configure the HTTP request pipeline.

            app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

            app.UseSerilogRequestLogging();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            app.UseAuthentication();

            app.UseMiddleware<FarmAccountAccessMiddleware>();

            app.UseMiddleware<AuthorizationFailureLoggingMiddleware>();

            app.UseAuthorization();

            app.UseRateLimiter();


            app.MapControllers();

            app.Run();
        }
    }
}
