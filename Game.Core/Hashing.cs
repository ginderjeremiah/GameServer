using System.Security.Cryptography;
using System.Text;

namespace Game.Core
{
    public static class Hashing
    {
        private const int ITERATIONS = 10000;
        private static byte[] _pepper = [];

        public static string Hash(this string input, string salt, int? iterations = ITERATIONS)
        {
            var saltAndPepper = AppendAll(Encoding.UTF8.GetBytes(salt), _pepper);
            var hashedBytes = SHA512.HashData(AppendAll(Encoding.UTF8.GetBytes(input), saltAndPepper));

            if (iterations > 1)
            {
                var input1 = new byte[hashedBytes.Length + saltAndPepper.Length];
                var input2 = new byte[hashedBytes.Length + saltAndPepper.Length];
                ReplaceFront(input1, saltAndPepper);
                ReplaceEnd(input2, saltAndPepper);

                for (int i = 1; i < iterations; i++)
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
            }

            return Convert.ToBase64String(hashedBytes);
        }

        /// <summary>
        /// Verifies that <paramref name="input"/> hashes to <paramref name="expectedHash"/> using the
        /// supplied <paramref name="salt"/>. The comparison is performed in constant time to avoid
        /// leaking information about the stored hash via timing side channels.
        /// </summary>
        public static bool VerifyHash(this string input, string salt, string expectedHash, int? iterations = ITERATIONS)
        {
            var computedHash = Encoding.UTF8.GetBytes(input.Hash(salt, iterations));
            var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);
            return CryptographicOperations.FixedTimeEquals(computedHash, expectedBytes);
        }

        public static void SetPepper(string pepper)
        {
            _pepper = Encoding.UTF8.GetBytes(pepper);
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
