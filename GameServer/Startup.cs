using DataAccess;
using GameLibrary;
using GameServer.Auth;
using System.Diagnostics;

namespace GameServer
{
    public class Startup
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var connectionString = builder.Configuration["ConnectionString"] ?? throw new Exception("Could not retrieve connection string.");
            var hashPepper = builder.Configuration["HashPepper"];
            if (hashPepper is not null)
            {
                Hashing.SetPepper(hashPepper);
            }
            var logger = new ApiLogger();

            logger.Log("Initializing CacheManager.");
            long startTime = Stopwatch.GetTimestamp();
            var cacheManager = new CacheManager(connectionString);
            logger.Log($"Initialized CacheManager: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds} ms");

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddTransient<IApiLogger>(sp => new ApiLogger());
            builder.Services.AddTransient<IRepositoryManager>(sp => new RepositoryManager(connectionString));
            builder.Services.AddSingleton<ICacheManager>(cacheManager);

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultChallengeScheme = "Default";
                options.AddScheme<SessionAuthHandler>("Default", "Default Scheme");
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Game}");

            app.Run();
        }
    }
}