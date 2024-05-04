using DataAccess;
using GameCore;
using GameCore.Database.Interfaces;
using GameCore.Logging.Interfaces;
using GameServer.Auth;
using GameServer.CodeGen;

namespace GameServer
{
    public class Startup
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var config = new Config(builder.Configuration);
            Hashing.SetPepper(config.HashPepper);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSingleton<ILogConfiguration>(config);
            builder.Services.AddSingleton<IDataConfiguration>(config);
            builder.Services.AddApiLogging();
            builder.Services.AddCacheProvider();
            builder.Services.AddPubSubProvider();
            builder.Services.AddDataProvider();
            builder.Services.AddTransient<IRepositoryManager, RepositoryManager>();

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultChallengeScheme = "SessionAuth";
                options.AddScheme<SessionAuthHandler>("SessionAuth", nameof(SessionAuthHandler));
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
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

            app.Run();
        }
    }
}