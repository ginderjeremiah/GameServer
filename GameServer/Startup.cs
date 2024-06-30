using DataAccess;
using GameCore;
using GameCore.Infrastructure;
using GameInfrastructure;
using GameServer.Auth;
using GameServer.CodeGen;
using GameServer.Services;
using System.Diagnostics;

namespace GameServer
{
    /// <summary>
    /// The class containing the entry point for the application.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// The entry point of the application.
        /// </summary>
        /// <param name="args">No parameters are currently supported.</param>
        /// <returns>An empty <see cref="Task"/>.</returns>
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var config = new Config(builder.Configuration);
            Hashing.SetPepper(config.HashPepper);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddEndpointsApiExplorer()
                .AddSwaggerGen()
                .AddSingleton<IDataServicesConfiguration, Config>()
                .AddScoped<IDataServicesFactory, DataServicesFactory>()
                .AddScoped(services => services.GetRequiredService<IDataServicesFactory>().Logger)
                .AddScoped<IRepositoryManager, RepositoryManager>()
                .AddScoped<SessionService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                var dataServices = new DataServicesFactory(config);
                var logger = dataServices.Logger;
                var start = Stopwatch.GetTimestamp();
                logger.LogDebug($"Beginning {nameof(dataServices.Database.EnsureDbUpdatedAsync)}");

                await dataServices.Database.EnsureDbUpdatedAsync();

                logger.LogDebug($"Finished {nameof(dataServices.Database.EnsureDbUpdatedAsync)}. Elapsed time {Stopwatch.GetElapsedTime(start)}");

                app.UseSwagger();
                app.UseSwaggerUI();
                ApiCodeGenerator.GenerateResponseInterfaces();
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
                pattern: "{controller=Home}/{action=Game}"
            );

            app.UseSessionAuth();

            app.Run();
        }
    }
}