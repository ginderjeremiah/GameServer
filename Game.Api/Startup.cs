using Game.Abstractions.Auth;
using Game.Abstractions.DataAccess;
using Game.Api.Auth;
using Game.Api.CodeGen;
using Game.Api.Events;
using Game.Api.Filters;
using Game.Api.Middleware;
using Game.Api.Services;
using Game.Api.Sockets.Commands;
using Game.Application.Auth;
using Game.Application.DependencyInjection;
using Game.Core.Events;
using Game.Core.Players.Events;
using Game.DataAccess;
using Game.DataAccess.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
        /// <param name="args">
        /// Pass <c>codegen [outputDirectory]</c> to regenerate the frontend's TypeScript API client
        /// and exit without starting the web host (see <see cref="CodeGenCommand"/>); otherwise no
        /// arguments are supported and the API starts normally.
        /// </param>
        /// <returns>An empty <see cref="Task"/>.</returns>
        public static async Task Main(string[] args)
        {
            // Standalone TypeScript codegen: regenerate the frontend API client without building the
            // web host or touching the database/cache, so types can be regenerated in CI / restricted
            // environments. Intercepted before host construction to stay independent of environment.
            if (CodeGenCommand.Matches(args))
            {
                CodeGenCommand.Run(args);
                return;
            }

            var builder = WebApplication.CreateBuilder(args);

            // Password-hashing parameters: the work factor binds from the optional "PasswordHashing"
            // section (default otherwise) and the required pepper from the top-level "HashPepper" secret.
            // Validated on start so a missing pepper fails fast, as the old eager check did.
            builder.Services.AddOptions<PasswordHashingOptions>()
                .Bind(builder.Configuration.GetSection("PasswordHashing"))
                .Configure(options => options.Pepper = builder.Configuration["HashPepper"] ?? string.Empty)
                .Validate(options => !string.IsNullOrEmpty(options.Pepper), "HashPepper not set")
                .ValidateOnStart();

            builder.Logging.ClearProviders()
                .AddSimpleConsole(options =>
                {
                    options.TimestampFormat = "HH:mm:ss.fff";
                });

            builder.Services.AddOptions<DataAccessOptions>()
                .BindConfiguration(nameof(DataAccessOptions));

            ConfigureAuth(builder);

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
                .AddDomainEventDispatcher()
                .AddApplication()
                .AddScoped<SessionService>()
                .AddSingleton<IAccessTokenService, JwtTokenService>()
                .AddSingleton<SocketConnectionRegistry>()
                .AddTransient<SocketManagerService>()
                .AddTransient<SocketCommandFactory>()
                .AddReferenceDataCommands()
                .AddSingleton<ApiCodeGenerator>()
                .AddScoped<AdminCacheReloadFilter>()
                .AddScoped<AdminRoleAuthorizationFilter>();

            // The socket registry is the shutdown hook that gracefully drains live player sockets (#526);
            // it shares the single singleton instance the SocketManagerService registers connections into.
            builder.Services.AddHostedService(sp => sp.GetRequiredService<SocketConnectionRegistry>());

            // Push a completed challenge's rewards to the player's live socket. This handler lives in the
            // API layer because it depends on the socket infrastructure, so it is registered here rather
            // than in AddApplication/AddDataAccess alongside the other domain-event handlers.
            DomainEventDispatcher.RegisterDomainEventHandler<ChallengeCompletedEvent, ChallengeCompletedNotifier>();

            // Migrations are applied on startup in Development (frictionless local F5) and whenever
            // DataAccessOptions:MigrateOnStartup is enabled (e.g. the Dockerized API used by CI and
            // end-to-end runs). This is intentionally decoupled from the Development-only TypeScript
            // codegen below, which writes into the frontend source tree.
            var migrateOnStartup = builder.Configuration.GetValue<bool>(
                $"{nameof(DataAccessOptions)}:{nameof(DataAccessOptions.MigrateOnStartup)}");

            if (builder.Environment.IsDevelopment() || migrateOnStartup)
            {
                builder.Services.AddDatabaseMigrator();
            }

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                // Regenerate the frontend's TypeScript API client from the running API's types. This
                // writes into the UI source tree, so it is strictly a local-development concern.
                var rootFolder = (Directory.GetParent(app.Environment.ContentRootPath)
                    ?? throw new InvalidOperationException($"Could not resolve parent directory of '{app.Environment.ContentRootPath}'.")).FullName;
                var targetDir = CodeGenPaths.ResolveTargetDirectory(rootFolder);
                var codeGen = app.Services.GetRequiredService<ApiCodeGenerator>();
                codeGen.GenerateCode(typeof(Startup).Assembly, new CodeGenOptions { TargetDirectory = targetDir, NewLine = "\n" });

                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            if (app.Environment.IsDevelopment() || migrateOnStartup)
            {
                // GameContext is Scoped, so the migrator must be resolved from a scope.
                using var migrationScope = app.Services.CreateScope();
                var migrator = migrationScope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
                await migrator.Migrate();
            }

            // Eagerly load the in-memory reference-data caches before serving traffic so a database
            // problem surfaces as a boot failure rather than on the first player request (#357), and so
            // every cache has a published snapshot before any read (#358).
            await app.Services.InitializeReferenceCachesAsync();

            app.UseCors(builder =>
            {
                builder.WithOrigins("http://localhost:5174")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });

            app.UseAuthentication();
            app.UseSessionLoader();
            app.UseRequestLogging();
            app.UseLoginTracking();
            app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });
            app.UseSocketInterceptor();
            app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }

        /// <summary>
        /// Configures standard ASP.NET Core JWT bearer authentication and authorization. Access tokens
        /// are validated against the configured signing key/issuer/audience and projected onto
        /// <see cref="System.Security.Claims.ClaimsPrincipal"/>. A fallback policy requires every endpoint
        /// to be authenticated unless explicitly marked <c>[AllowAnonymous]</c>.
        /// </summary>
        private static void ConfigureAuth(WebApplicationBuilder builder)
        {
            var jwtSection = builder.Configuration.GetSection("Jwt");
            builder.Services.AddOptions<JwtOptions>().Bind(jwtSection);

            var signingKey = jwtSection["SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey not set");
            var issuer = jwtSection["Issuer"] ?? Constants.SERVER_PRINCIPAL;
            var audience = jwtSection["Audience"] ?? Constants.SERVER_PRINCIPAL;

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // Keep claim types verbatim ("sub"/"role") rather than remapping to legacy URIs.
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        ValidateAudience = true,
                        ValidAudience = audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        NameClaimType = JwtRegisteredClaimNames.Sub,
                        RoleClaimType = JwtTokenService.RoleClaimType,
                    };
                    // Browsers can't set Authorization headers on the WebSocket handshake, so the socket
                    // endpoint accepts the access token via the access_token query-string parameter
                    // (the standard ASP.NET Core pattern for token auth over WebSockets).
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            if (string.IsNullOrEmpty(context.Token) && context.HttpContext.Request.Path == "/socket")
                            {
                                var queryToken = context.Request.Query["access_token"];
                                if (!string.IsNullOrEmpty(queryToken))
                                {
                                    context.Token = queryToken;
                                }
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });
        }
    }
}
