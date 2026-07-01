using Game.Abstractions.Auth;
using Game.Abstractions.Content;
using Game.Abstractions.DataAccess;
using Game.Api.Auth;
using Game.Api.CodeGen;
using Game.Api.Cors;
using Game.Api.Events;
using Game.Api.Filters;
using Game.Api.Forwarding;
using Game.Api.Middleware;
using Game.Api.RateLimiting;
using Game.Api.Services;
using Game.Api.Sockets.Commands;
using Game.Application.Auth;
using Game.Application.DependencyInjection;
using Game.Application.Services;
using Game.Core.Events;
using Game.Core.Players.Events;
using Game.DataAccess;
using Game.DataAccess.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
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

            // Allowed CORS origins are deployment-specific; the local dev origin is supplied in
            // appsettings.Development.json. Validated on start so a deployment with no configured origin
            // fails fast rather than silently rejecting every browser request.
            builder.Services.AddOptions<CorsOptions>()
                .BindConfiguration(CorsOptions.SectionName)
                .Validate(options => options.AllowedOrigins.Count > 0, "Cors:AllowedOrigins not set")
                .ValidateOnStart();

            // Trusted reverse proxies whose X-Forwarded-For is honoured are deployment-specific and default
            // to empty (trust nothing). The pipeline below only enables the forwarded-headers middleware
            // when at least one is configured, so by default a spoofed X-Forwarded-For from a direct client
            // is ignored and the recorded IP stays the socket peer address (#910).
            builder.Services.AddOptions<ForwardedHeadersConfig>()
                .BindConfiguration(ForwardedHeadersConfig.SectionName)
                // ForwardLimit is the trust-chain depth: null means "no limit" (walk every entry), otherwise
                // it must be at least 1. Validated on start so a misconfigured zero/negative fails fast rather
                // than silently dropping all forwarded entries even when trusted proxies are configured.
                .Validate(
                    options => options.ForwardLimit is null || options.ForwardLimit >= 1,
                    "ForwardedHeaders:ForwardLimit must be null (no limit) or at least 1")
                .ValidateOnStart();
            builder.Services.AddOptions<ForwardedHeadersOptions>()
                .Configure<IOptions<ForwardedHeadersConfig>>((options, config) => config.Value.Apply(options));

            // Throttle the anonymous auth endpoints per client IP to blunt credential stuffing, refresh-token
            // brute force, and the PBKDF2 resource-exhaustion vector. Limits are config-bound with safe
            // defaults so an unconfigured deployment is still protected (#950).
            builder.Services.AddAuthRateLimiter();

            // Per-account exponential login backoff layered on top of the IP limiter: defence-in-depth against
            // a slow, distributed credential guess on one account that stays under any single IP's rate limit.
            // Config-bound with safe defaults (like the IP limiter) and validated as sane on start so a
            // misconfiguration fails fast rather than silently disabling or inverting the guard (#1010).
            builder.Services.AddOptions<LoginBackoffOptions>()
                .BindConfiguration(LoginBackoffOptions.SectionName)
                .Validate(
                    options => options.FailureThreshold >= 0
                        && options.BaseDelaySeconds > 0
                        && options.MaxDelaySeconds >= options.BaseDelaySeconds
                        && options.FailureWindowSeconds > 0,
                    "LoginBackoff requires FailureThreshold >= 0, 0 < BaseDelaySeconds <= MaxDelaySeconds, and FailureWindowSeconds > 0")
                .ValidateOnStart();

            // The per-account character cap (anti-cheat) is config-bound with a safe positive default, so an
            // unconfigured deployment still enforces a cap; validated as positive on start so a misconfigured
            // zero/negative fails fast rather than silently disabling the guard.
            builder.Services.AddOptions<PlayerCreationOptions>()
                .BindConfiguration(PlayerCreationOptions.SectionName)
                .Validate(
                    options => options.MaxPlayersPerAccount > 0,
                    "PlayerCreation requires MaxPlayersPerAccount > 0")
                .ValidateOnStart();

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
                .AddScoped<SessionInitializer>()
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

            // Push a won battle's proficiency-XP gains (level-ups, milestones) to the player's live socket,
            // for the same socket-infrastructure reason as the challenge notifier above.
            DomainEventDispatcher.RegisterDomainEventHandler<ProficiencyXpGainedEvent, ProficiencyXpNotifier>();

            // Migrations are applied on startup in Development (frictionless local F5) and whenever
            // DataAccessOptions:MigrateOnStartup is enabled (e.g. the Dockerized API used by CI and
            // end-to-end runs). This is intentionally decoupled from the Development-only TypeScript
            // codegen below, which writes into the frontend source tree.
            var migrateOnStartup = builder.Configuration.GetValue<bool>(
                $"{nameof(DataAccessOptions)}:{nameof(DataAccessOptions.MigrateOnStartup)}");

            // Seed the static content from the source-controlled export after migrating a fresh database
            // (dev / CI / recovery), so it has a real content baseline. Idempotent (skips a populated DB).
            // Development always seeds; other environments opt in. Seeding needs a migrated schema, so it is
            // gated on migration running too.
            var seedContentOnStartup = builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>(
                $"{nameof(DataAccessOptions)}:{nameof(DataAccessOptions.SeedContentOnStartup)}");

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
                // GameContext is Scoped, so the migrator and seeder must be resolved from a scope.
                using var migrationScope = app.Services.CreateScope();
                var migrator = migrationScope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
                await migrator.Migrate();

                // Seed the static content into a freshly-migrated database (no-op when it already has content),
                // before the reference caches load below so their eager build picks the seed up.
                if (seedContentOnStartup)
                {
                    var reader = migrationScope.ServiceProvider.GetRequiredService<IContentImportReader>();
                    var seeder = migrationScope.ServiceProvider.GetRequiredService<IContentSeeder>();
                    await seeder.SeedAsync(reader.ReadDefault());
                }
            }

            var corsOptions = app.Services.GetRequiredService<IOptions<CorsOptions>>().Value;

            // Eagerly load the in-memory reference-data caches before serving traffic so a database
            // problem surfaces as a boot failure rather than on the first player request (#357), and so
            // every cache has a published snapshot before any read (#358).
            await app.Services.InitializeReferenceCachesAsync();

            // Apply X-Forwarded-For first so RemoteIpAddress reflects the real client for everything
            // downstream (login tracking, logging). Run the middleware only when trusted proxies are
            // configured: with an empty allowlist it would skip its known-proxy check and trust the header
            // unconditionally, so leaving it off keeps RemoteIpAddress as the socket peer and a spoofed
            // header is ignored (#910).
            var forwardedHeadersConfig = app.Services.GetRequiredService<IOptions<ForwardedHeadersConfig>>().Value;
            if (forwardedHeadersConfig.HasTrustedProxies)
            {
                app.UseForwardedHeaders();
            }

            app.UseCors(builder =>
            {
                builder.WithOrigins([.. corsOptions.AllowedOrigins])
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });

            // After CORS so a throttled browser request still carries the CORS headers needed to read the
            // 429 envelope, and after forwarded-headers (above) so the partition keys off the real client IP.
            app.UseRateLimiter();

            app.UseAuthentication();
            app.UseSessionLoader();
            app.UseRequestLogging();
            // Runs inside request logging so an unhandled exception is converted to a 500 (with the
            // consistent error envelope) before the request-ended event logs the response status.
            app.UseExceptionHandling();
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
            builder.Services.AddOptions<JwtOptions>()
                .Bind(jwtSection)
                // HMAC-SHA256 needs a key of at least its 256-bit output, so fail fast at startup (mirroring
                // the pepper/CORS validation below) rather than only when the first token is issued.
                .Validate(options => Encoding.UTF8.GetByteCount(options.SigningKey) >= 32, "Jwt:SigningKey must be at least 32 bytes for HMAC-SHA256")
                .ValidateOnStart();

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
