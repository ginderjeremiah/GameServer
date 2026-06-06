namespace Game.Api.CodeGen
{
    /// <summary>
    /// Standalone entry point for the TypeScript API client code generation. Runs the same
    /// reflection-based generation as the dev-time startup hook, but without building the web host or
    /// touching any out-of-process dependency (database/cache), so the client types can be
    /// regenerated in CI or restricted environments via
    /// <c>dotnet run --project Game.Api -- codegen [outputDirectory]</c>.
    /// </summary>
    public static class CodeGenCommand
    {
        /// <summary>The CLI verb that selects this command.</summary>
        public const string CommandName = "codegen";

        /// <summary>
        /// Returns whether the supplied process arguments invoke the standalone code generator.
        /// </summary>
        public static bool Matches(string[] args)
        {
            return args.Length > 0 && string.Equals(args[0], CommandName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Runs code generation against the API assembly. An optional output directory may be supplied
        /// as the second argument; when omitted, the frontend types directory is resolved relative to
        /// the repository root.
        /// </summary>
        public static void Run(string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole());
            var logger = loggerFactory.CreateLogger<ApiCodeGenerator>();

            var targetDirectory = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
                ? Path.GetFullPath(args[1])
                : CodeGenPaths.ResolveTargetDirectory(CodeGenPaths.FindRepositoryRoot());

            new ApiCodeGenerator(logger).GenerateCode(
                typeof(Startup).Assembly,
                new CodeGenOptions { TargetDirectory = targetDirectory, NewLine = "\n" });

            logger.LogInformation("Generated TypeScript API client into {TargetDirectory}.", targetDirectory);
        }
    }
}
