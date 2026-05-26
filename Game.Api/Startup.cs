using Game.Abstractions.DataAccess;
using Game.Api.CodeGen;
using Game.Api.Filters;
using Game.Api.Middleware;
using Game.Api.Services;
using Game.Application;
using Game.Application.Services;
using Game.Core;
using Game.DataAccess;
using Game.DataAccess.DependencyInjection;

namespace Game.Api
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
            Hashing.SetPepper(builder.Configuration["HashPepper"] ?? throw new InvalidOperationException("HashPepper not set"));

            builder.Logging.ClearProviders()
                .AddSimpleConsole(options =>
                {
                    options.TimestampFormat = "HH:mm:ss.fff";
                });

            //.AddConsole(options => options.FormatterName = nameof(LogFormatter))
            //.AddConsoleFormatter<LogFormatter, LogFormatterOptions>();

            builder.Services.AddOptions<DataAccessOptions>()
                .BindConfiguration(nameof(DataAccessOptions));

            // Add services to the container.
            builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ErrorStatusFilter>();
                options.Filters.Add<CommitFilter>();
            });
            builder.Services.AddEndpointsApiExplorer()
                .AddSwaggerGen()
                .AddHttpContextAccessor()
                .AddDataAccess()
                .AddScoped<SessionService>()
                .AddScoped<CookieService>()
                .AddTransient<SocketManagerService>()
                .AddTransient<SocketCommandFactory>()
                .AddSingleton<ApiCodeGenerator>()
                // Application services
                .AddScoped<BattlerFactory>()
                .AddScoped<BattleService>()
                .AddScoped<PlayerService>()
                .AddScoped<StatisticsService>()
                .AddScoped<ChallengeService>()
                // Domain event infrastructure
                .AddScoped<AdminCacheInvalidationFilter>()
                .AddScoped<IDomainEventDispatcher, DomainEventDispatcher>()
                .AddScoped<IDomainEventHandler, LoggingEventHandler>()
                .AddScoped<IDomainEventHandler, StatisticsEventHandler>()
                .AddScoped<IDomainEventHandler, BattleStatisticsEventHandler>();

            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddDatabaseMigrator();
            }

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                var rootFolder = Directory.GetParent(app.Environment.ContentRootPath)!.FullName;
                var targetDir = $"{rootFolder}\\UI\\new-svelte\\src\\lib\\api\\types";
                var codeGen = app.Services.GetRequiredService<ApiCodeGenerator>();
                codeGen.GenerateCode(typeof(Startup).Assembly, targetDir);

                // GameContext is Scoped, so the migrator must be resolved from a scope.
                using var migrationScope = app.Services.CreateScope();
                var migrator = migrationScope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
                await migrator.Migrate(true);

                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseCors(builder =>
            {
                builder.WithOrigins("http://localhost:5174")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });

            //app.UseHttpsRedirection();
            app.UseTokenAuth();
            app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });
            app.UseSocketInterceptor();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}