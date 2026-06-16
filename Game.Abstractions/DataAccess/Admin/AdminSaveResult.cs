namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// The outcome of an admin Content-Authoring write. Every admin save/setter reports success or
    /// failure through this one shape — a <c>null</c> <see cref="ErrorMessage"/> is success, a non-null
    /// one is a user-facing rejection the controller surfaces verbatim — so the API maps the result to an
    /// <c>ApiResponse</c> in one place instead of each endpoint re-deriving the bool→response mapping.
    /// </summary>
    public sealed class AdminSaveResult
    {
        private AdminSaveResult(string? errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        /// <summary>The user-facing failure message, or <c>null</c> when the write succeeded.</summary>
        public string? ErrorMessage { get; }

        /// <summary>True when the write succeeded (no error message).</summary>
        public bool Succeeded => ErrorMessage is null;

        /// <summary>A successful write.</summary>
        public static AdminSaveResult Success { get; } = new(null);

        /// <summary>A rejection because the targeted <paramref name="entity"/> does not exist
        /// (e.g. <c>"Enemy"</c> → <c>"Enemy not found."</c>) — the single place the not-found copy is formatted.</summary>
        public static AdminSaveResult NotFound(string entity) => new($"{entity} not found.");

        /// <summary>A rejection with an explicit user-facing <paramref name="message"/>.</summary>
        public static AdminSaveResult Failure(string message) => new(message);
    }
}
