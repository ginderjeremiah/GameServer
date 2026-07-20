using System.Diagnostics.CodeAnalysis;

namespace Game.Core.Players
{
    /// <summary>
    /// Validation rules for a player-supplied character name. Names need not be globally unique (the game
    /// has no competitive surface), so the rules only bound the length and reject blank or control-character
    /// names. <see cref="MaxLength"/> mirrors the persisted <c>Player.Name</c> column so a valid name always
    /// fits storage. This is the domain answer to "is this an acceptable character name?", enforced
    /// server-side as anti-cheat regardless of any client-side check.
    /// </summary>
    public static class PlayerName
    {
        /// <summary>The shortest a (trimmed) name may be.</summary>
        public const int MinLength = 1;

        /// <summary>The longest a (trimmed) name may be — mirrors the persisted <c>Player.Name</c> column.</summary>
        public const int MaxLength = 20;

        /// <summary>
        /// Validates and normalizes a player-supplied name: trims surrounding whitespace, then accepts it
        /// only when the trimmed value is within [<see cref="MinLength"/>, <see cref="MaxLength"/>] and free
        /// of control characters. Returns the normalized name via <paramref name="normalized"/> when valid.
        /// Deliberately does <b>not</b> reject zero-width/Unicode "Format"-category characters the way
        /// <see cref="Game.Core.Identity.UsernamePolicy.TryNormalize"/> does — character names are
        /// non-unique with no admin/competitive surface, so a visually-confusable duplicate isn't a
        /// spoofing risk the way it is for logins. Not an oversight; keep in sync only if that changes.
        /// </summary>
        public static bool TryNormalize(string? name, [NotNullWhen(true)] out string? normalized)
        {
            normalized = null;
            if (name is null)
            {
                return false;
            }

            var trimmed = name.Trim();
            if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
            {
                return false;
            }

            if (trimmed.Any(char.IsControl))
            {
                return false;
            }

            normalized = trimmed;
            return true;
        }
    }
}
