namespace Game.Core.Identity
{
    /// <summary>
    /// Validation rules for a login password. This is the domain answer to "is this an acceptable
    /// password?", enforced server-side as anti-cheat/defense-in-depth regardless of any client-side
    /// check — <c>AccountService.CreateAccount</c> otherwise hashes whatever it is given. Mirrors the
    /// signup-mode rule already enforced client-side in <c>login-validation.ts</c>'s
    /// <c>validatePassword</c>, so a password accepted by the signup form is never rejected by the server.
    /// </summary>
    public static class PasswordPolicy
    {
        /// <summary>The shortest a password may be.</summary>
        public const int MinLength = 8;

        /// <summary>The longest a password may be — mirrors the <see cref="Api.Models.Auth.CreateAccountRequest"/> length bound.</summary>
        public const int MaxLength = 256;

        /// <summary>
        /// Validates a caller-supplied password: accepts it only when it is within
        /// [<see cref="MinLength"/>, <see cref="MaxLength"/>] and contains at least one letter and one
        /// digit. Deliberately does <b>not</b> trim or otherwise normalize the value — unlike a username
        /// or character name, password whitespace is meaningful and must be hashed exactly as supplied.
        /// </summary>
        public static bool IsValid(string? password)
        {
            if (password is null || password.Length < MinLength || password.Length > MaxLength)
            {
                return false;
            }

            return password.Any(char.IsLetter) && password.Any(char.IsDigit);
        }
    }
}
