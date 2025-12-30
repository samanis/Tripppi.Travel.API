
using Microsoft.EntityFrameworkCore;
using Tripppi.Travel.API.Infrastructure;
using Tripppi.Travel.API.Repositories;
using Tripppi.Travel.API.Services;

namespace Tripppi.Travel.API
{
    public partial class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var builder = WebApplication.CreateBuilder(args);

            // Configure logging to show all errors
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);

            // Configure PostgreSQL with Npgsql
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Register repositories
            builder.Services.AddScoped<IAirportRepository, EfAirportRepository>();

            // Register application services
            builder.Services.AddScoped<ILocationService, LocationService>();

            // In-memory cache for startup-loaded airport data
            builder.Services.AddMemoryCache();

            // configure cache options and register preloader
            builder.Services.Configure<AirportCacheOptions>(builder.Configuration.GetSection("Cache"));
            // TEMPORARILY DISABLED to test if hosted service is causing crash
            // builder.Services.AddHostedService<AirportCachePreloader>();

            // CORS: Allow only client app origin (configured per environment)
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowClientApp", policy =>
                {
                    var allowedOrigin = builder.Configuration["Security:AllowedOrigin"];
                    
                    if (string.IsNullOrWhiteSpace(allowedOrigin))
                    {
                        // Development: allow all origins (for testing only)
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    }
                    else
                    {
                        // Production: strict origin check
                        policy.WithOrigins(allowedOrigin)
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials();
                    }
                });
            });

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Global exception handler - temporarily disabled for debugging
            // app.UseMiddleware<Tripppi.Travel.API.Middleware.GlobalExceptionHandlerMiddleware>();

            // Origin secret validation: block requests not from CloudFront
            app.UseMiddleware<Tripppi.Travel.API.Middleware.OriginSecretValidationMiddleware>();

            // CORS: only allow configured client app origin
            app.UseCors("AllowClientApp");

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
