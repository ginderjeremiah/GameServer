using DataAccess;
using GameLibrary;
using GameServer.Auth;

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
            builder.Services.AddSingleton<IDataConfiguration>(config);
            builder.Services.AddTransient<IApiLogger, ApiLogger>();
            builder.Services.AddTransient<IRepositoryManager, RepositoryManager>();

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultChallengeScheme = "Default";
                options.AddScheme<SessionAuthHandler>("Default", nameof(SessionAuthHandler));
            });

            //builder.Services.AddSession((options) =>
            //{
            //    options.Cookie
            //});

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