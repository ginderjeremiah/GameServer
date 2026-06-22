using System.Text.Json;
using System.Text.RegularExpressions;

namespace Game.Core
{
    /// <summary>
    /// Generic extension methods.
    /// </summary>
    public static partial class Extensions
    {
        private static readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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
        public static IEnumerable<T1> WhereNotNull<T1>(this IEnumerable<T1?> source)
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
        /// Returns only the elements that are not null.
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IEnumerable<T1> WhereNotNull<T1>(this IEnumerable<Nullable<T1>> source) where T1 : struct
        {
            foreach (var item in source)
            {
                if (item is not null)
                {
                    yield return item.Value;
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
            return source.Select(selector).WhereNotNull();
        }

        /// <summary>
        /// <inheritdoc cref="Enumerable.Select{TSource, TResult}(IEnumerable{TSource}, Func{TSource, TResult})"/> Then discards any results which are null.
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="source"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<T2> SelectNotNull<T1, T2>(this IEnumerable<T1> source, Func<T1, Nullable<T2>> selector) where T2 : struct
        {
            return source.Select(selector).WhereNotNull();
        }

        /// <summary>
        /// Returns a copy of this string, but the first letter is uppercase. An empty string is returned unchanged.
        /// </summary>
        /// <param name="str"></param>
        /// <returns>A string with the first letter uppercase.</returns>
        public static string Capitalize(this string str)
        {
            return str.Length == 0 ? str : string.Concat(str[0].ToString().ToUpper(), str.AsSpan(1));
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

        [GeneratedRegex("([a-z])([A-Z])")]
        private static partial Regex WordBreakRegex();
    }
}
