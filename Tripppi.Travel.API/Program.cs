
using Microsoft.EntityFrameworkCore;
using Tripppi.Travel.API.Infrastructure;
using Tripppi.Travel.API.Repositories;
using Tripppi.Travel.API.Services;
using Tripppi.Travel.API.Logging;
using AWS.Logger;
using AWS.Logger.AspNetCore;

namespace Tripppi.Travel.API
{
    public partial class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var builder = WebApplication.CreateBuilder(args);

            // AWS Configuration: Load secrets from environment variables (populated by ECS from Secrets Manager)
            if (builder.Environment.IsProduction())
            {
                // Override connection string from environment variable
                var dbConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
                if (!string.IsNullOrEmpty(dbConnectionString))
                {
                    builder.Configuration["ConnectionStrings:DefaultConnection"] = dbConnectionString;
                }

                // Override origin secret from environment variable
                var originSecret = Environment.GetEnvironmentVariable("ORIGIN_SECRET");
                if (!string.IsNullOrEmpty(originSecret))
                {
                    builder.Configuration["Security:OriginSecret"] = originSecret;
                }
            }

            // Configure logging: Console for dev, AWS CloudWatch for production
            builder.Logging.ClearProviders();
            
            if (builder.Environment.IsProduction())
            {
                // IMPORTANT: AWS Logger in containers requires console logging for now
                // CloudWatch integration via AWS Logger has credential discovery issues in Docker containers
                // even with host networking and proper IAM roles.
                // 
                // For CloudWatch logs in production, use one of these alternatives:
                // 1. AWS CloudWatch Agent on the host collecting Docker logs
                // 2. Fluent Bit sidecar container
                // 3. AWS ECS with awslogs driver (for ECS deployments)
                //
                // For now: console logs + CloudWatch Agent or external log shipping
                builder.Logging.AddConsole();
                builder.Logging.SetMinimumLevel(LogLevel.Information);
                Console.WriteLine("ℹ️  Production logging: Console output (for Docker log collection)");
            }
            else
            {
                // Development: console logging with debug level
                builder.Logging.AddConsole();
                builder.Logging.SetMinimumLevel(LogLevel.Debug);
            }

            // Configure PostgreSQL with Npgsql
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Register repositories
            builder.Services.AddScoped<IAirportRepository, EfAirportRepository>();

            // Register application services
            builder.Services.AddScoped<ILocationService, LocationService>();

            // Register logging abstraction (platform-agnostic wrapper)
            builder.Services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));

            // In-memory cache for startup-loaded airport data
            builder.Services.AddMemoryCache();

            // configure cache options and register preloader
            builder.Services.Configure<AirportCacheOptions>(builder.Configuration.GetSection("Cache"));
            
            // Enable cache preloader in production (disabled in dev for faster startup)
            if (builder.Environment.IsProduction())
            {
                builder.Services.AddHostedService<AirportCachePreloader>();
            }

            // CORS: Required by Figma - Allow all origins
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowClientApp", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Global exception handler
            app.UseMiddleware<Tripppi.Travel.API.Middleware.GlobalExceptionHandlerMiddleware>();

            // Request/response logging (logs all incoming requests and responses)
            app.UseMiddleware<Tripppi.Travel.API.Middleware.RequestResponseLoggingMiddleware>();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Only redirect HTTPS in production, not in development
            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            // CORS MUST be before UseAuthorization and routing
            // Required by Figma: Access-Control-Allow-Origin: *, Methods: GET/OPTIONS, Headers: Accept/Content-Type
            app.UseCors("AllowClientApp");
            
            app.UseAuthorization();

            // Minimal test endpoint with zero dependencies
            app.MapGet("/test", () => "Hello World");

            app.MapControllers();

                app.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n\n=== FATAL ERROR ===\n{ex}\n===================\n");
                throw;
            }
        }
    }
}
