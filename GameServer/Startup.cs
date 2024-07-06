using DataAccess;
using GameCore;
using GameCore.DataAccess;
using GameInfrastructure;
using GameServer.Auth;
using GameServer.CodeGen;
using GameServer.Services;
using GameServer.Sockets;
using Microsoft.EntityFrameworkCore;
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
                .AddTransient<IDataServicesFactory, DataServicesFactory>()
                .AddTransient(services => services.GetRequiredService<IDataServicesFactory>().Logger)
                .AddTransient<IRepositoryManager, RepositoryManager>()
                .AddScoped<SessionService>()
                .AddTransient<SocketManagerService>()
                .AddTransient<SocketCommandFactory>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                var dataServices = new DataServicesFactory(config);
                var logger = dataServices.Logger;
                var start = Stopwatch.GetTimestamp();
                logger.LogDebug($"Beginning migration.");

                await dataServices.DbContext.Database.MigrateAsync();

                logger.LogDebug($"Finished migration. Elapsed time {Stopwatch.GetElapsedTime(start)}");

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
            app.UseSessionAuth();
            app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });
            app.UseSocketInterceptor();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Game}"
            );

            app.Run();
        }
    }
}