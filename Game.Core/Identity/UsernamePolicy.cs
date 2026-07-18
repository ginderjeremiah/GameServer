using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Game.Core.Identity
{
    /// <summary>
    /// Validation rules for a login username. <see cref="MaxLength"/> mirrors the persisted
    /// <c>User.Username</c> column (see <see cref="Game.Core.Players.PlayerName"/> for the equivalent
    /// player-name rule) so a valid username always fits storage. Named <c>UsernamePolicy</c> rather than
    /// <c>Username</c> so it doesn't collide with the <c>Username</c> property on the request models that
    /// reference it. This is the domain answer to "is this an acceptable username?", enforced server-side
    /// as anti-cheat regardless of any client-side check — it guards the admin roster, ban/archive surface,
    /// and logs against confusable/spoofed entries (e.g. an account visually identical to an admin's).
    /// </summary>
    public static class UsernamePolicy
    {
        /// <summary>The shortest a (trimmed) username may be.</summary>
        public const int MinLength = 1;

        /// <summary>The longest a (trimmed) username may be — mirrors the persisted <c>User.Username</c> column.</summary>
        public const int MaxLength = 20;

        /// <summary>
        /// Validates and normalizes a caller-supplied username: trims surrounding whitespace, then accepts
        /// it only when the trimmed value is within [<see cref="MinLength"/>, <see cref="MaxLength"/>] and
        /// free of control and zero-width characters (which could otherwise render a username visually
        /// identical to another). Returns the normalized username via <paramref name="normalized"/> when valid.
        /// </summary>
        public static bool TryNormalize(string? username, [NotNullWhen(true)] out string? normalized)
        {
            normalized = null;
            if (username is null)
            {
                return false;
            }

            var trimmed = username.Trim();
            if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
            {
                return false;
            }

            if (trimmed.Any(IsDisallowedCharacter))
            {
                return false;
            }

            normalized = trimmed;
            return true;
        }

        // Control characters (e.g. tab, null) and the Unicode "Format" category (e.g. zero-width space,
        // zero-width joiner/non-joiner, the zero-width no-break space/BOM) are both invisible and can make
        // two usernames render identically while being distinct strings.
        private static bool IsDisallowedCharacter(char c)
        {
            return char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format;
        }
    }
}
