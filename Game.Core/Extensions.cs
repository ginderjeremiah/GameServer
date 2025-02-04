using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Game.Core.EAttribute;

namespace Game.Core
{
    /// <summary>
    /// Generic extension methods.
    /// </summary>
    public static partial class Extensions
    {
        private static readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        /// <summary>
        /// Converts this object to a string using its <see cref="object.ToString"/> method then converts that to a base64 string representation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns>A base64 <see cref="string"/> representation of this object.</returns>
        public static string ToBase64<T>(this T obj)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(obj?.ToString() ?? ""));
        }

        /// <summary>
        /// Converts this string from base64 to a UTF8 encoding representation.
        /// </summary>
        /// <param name="str"></param>
        /// <returns>This string in a UTF8 representation.</returns>
        public static string FromBase64(this string str)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(str));
        }

        /// <summary>
        /// Reads the <see cref="HttpRequestMessage.Content"/> as a JSON stream and deserializes it to an object of type <typeparamref name="T"/>.
        /// </summary>
        public static T? Deserialize<T>(this HttpResponseMessage msg)
        {
            var stream = msg.Content.ReadAsStream();
            return stream.Length > 0 ? JsonSerializer.Deserialize<T>(stream, _options) : default;
        }

        /// <summary>
        /// Deserializes this string from JSON to an object of type <typeparamref name="T"/>.
        /// </summary>
        public static T? Deserialize<T>(this string? str)
        {
            return str is null ? default : JsonSerializer.Deserialize<T>(str, _options);
        }

        /// <summary>
        /// Serializes this object to a JSON string.
        /// </summary>
        public static string Serialize<T>(this T obj)
        {
            return JsonSerializer.Serialize(obj, _options);
        }

        /// <summary>
        /// Returns only the elements that are not null.
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IEnumerable<T1> SelectNotNull<T1>(this IEnumerable<T1?> source)
        {
            foreach (var item in source)
            {
                if (item is not null)
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// <inheritdoc cref="Enumerable.Select{TSource, TResult}(IEnumerable{TSource}, Func{TSource, TResult})"/> Then discards any results which are null.
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="source"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<T2> SelectNotNull<T1, T2>(this IEnumerable<T1> source, Func<T1, T2?> selector)
        {
            return source.Select(selector).SelectNotNull();
        }

        /// <summary>
        /// Returns a copy of this string, but the first letter is lowercase.
        /// </summary>
        /// <param name="str"></param>
        /// <returns>A string with the first letter lowercase.</returns>
        public static string Decapitalize(this string str)
        {
            return string.Concat(str[0].ToString().ToLower(), str.AsSpan(1));
        }

        /// <summary>
        /// Returns a copy of this string, but the first letter is uppercase.
        /// </summary>
        /// <param name="str"></param>
        /// <returns>A string with the first letter uppercase.</returns>
        public static string Capitalize(this string str)
        {
            return string.Concat(str[0].ToString().ToUpper(), str.AsSpan(1));
        }

        /// <summary>
        /// Returns a copy of this string, but each sequence of a lowercase character followed by an uppercase character has a space inserted between the characters.
        /// </summary>
        /// <param name="str"></param>
        /// <returns>A string with spaces between each lowercase character followed by an uppercase character.</returns>
        public static string SpaceWords(this string str)
        {
            return WordBreakRegex().Replace(str, "$1 $2");
        }

        /// <summary>
        /// Returns a copy of this string, but each sequence of a lowercase character followed by an uppercase character has a "-" inserted between the characters
        /// then the entire string is converted to lowercase.
        /// </summary>
        /// <param name="str"></param>
        /// <returns>A string in lowercase with "-"s between each lowercase character followed by an uppercase character in the original string.</returns>
        public static string SnakeCase(this string str)
        {
            return WordBreakRegex().Replace(str, "$1-$2").ToLower();
        }

        /// <summary>
        /// Returns true if this is one of the core attributes: <see cref="Strength"/>, <see cref="Endurance"/>, <see cref="Intellect"/>,
        /// <see cref="Agility"/>, <see cref="Dexterity"/>, or <see cref="Luck"/>.
        /// </summary>
        /// <param name="att"></param>
        /// <returns></returns>
        public static bool IsCoreAttribute(this EAttribute att)
        {
            return att is Strength or Endurance or Intellect or Agility or Dexterity or Luck;
        }

        [GeneratedRegex("([a-z])([A-Z])")]
        private static partial Regex WordBreakRegex();
    }
}