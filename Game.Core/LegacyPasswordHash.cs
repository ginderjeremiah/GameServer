using System.Security.Cryptography;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// The original bespoke iterated-SHA-512 password hash. It is retained <b>only</b> to verify (and
    /// thereby transparently migrate, on next login) credentials created before the switch to PBKDF2 —
    /// it must not be used to hash new passwords. See docs/backend.md (Authentication → Password hashing).
    /// </summary>
    /// <remarks>
    /// Unlike the previous version this is a pure function: the pepper is supplied by the caller rather
    /// than held in mutable static state, so there is no process-global setup to perform.
    /// </remarks>
    public static class LegacyPasswordHash
    {
        private const int Iterations = 10000;

        /// <summary>
        /// Reproduces the legacy hash of <paramref name="input"/> for the given <paramref name="salt"/>
        /// and <paramref name="pepper"/>, exactly as stored by the pre-PBKDF2 scheme, so a legacy
        /// credential can be verified before being re-hashed.
        /// </summary>
        public static string Hash(string input, string salt, byte[] pepper)
        {
            var saltAndPepper = AppendAll(Encoding.UTF8.GetBytes(salt), pepper);
            var hashedBytes = SHA512.HashData(AppendAll(Encoding.UTF8.GetBytes(input), saltAndPepper));

            var input1 = new byte[hashedBytes.Length + saltAndPepper.Length];
            var input2 = new byte[hashedBytes.Length + saltAndPepper.Length];
            ReplaceFront(input1, saltAndPepper);
            ReplaceEnd(input2, saltAndPepper);

            for (int i = 1; i < Iterations; i++)
            {
                if (i % 2 == 0)
                {
                    ReplaceEnd(input1, hashedBytes);
                    hashedBytes = SHA512.HashData(input1);
                }
                else
                {
                    ReplaceFront(input2, hashedBytes);
                    hashedBytes = SHA512.HashData(input2);
                }
            }

            return Convert.ToBase64String(hashedBytes);
        }

        private static void ReplaceFront(byte[] original, byte[] replace)
        {
            for (int i = 0; i < replace.Length; i++)
            {
                original[i] = replace[i];
            }
        }

        private static void ReplaceEnd(byte[] original, byte[] replace)
        {
            var dif = original.Length - replace.Length;
            for (int i = 0; i < replace.Length; i++)
            {
                original[dif + i] = replace[i];
            }
        }

        private static T[] AppendAll<T>(T[] first, T[] second)
        {
            T[] output = new T[first.Length + second.Length];
            for (int i = 0; i < first.Length; i++)
            {
                output[i] = first[i];
            }

            for (int i = 0; i < second.Length; i++)
            {
                output[first.Length + i] = second[i];
            }

            return output;
        }
    }
}
