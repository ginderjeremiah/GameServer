using GameCore.Entities;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static GameCore.EAttribute;

namespace GameCore
{
    public static partial class Extensions
    {
        private static readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static string AsString(this object? obj, string defaultVal = "")
        {
            return obj switch
            {
                IEnumerable<object?> enumerable => string.Join(",", enumerable.Select(o => o.AsString())),
                _ => obj?.ToString() ?? defaultVal,
            };
        }

        public static int AsInt(this object? obj, int defaultVal = default)
        {
            return obj is int intObj ? intObj : int.TryParse(obj.AsString(), out var val) ? val : defaultVal;
        }

        public static DateTime AsDate(this object? obj, DateTime? defaultVal = null)
        {
            return obj is DateTime dtObj ? dtObj : DateTime.TryParse(obj.AsString(), out var dt) ? dt : defaultVal ?? DateTime.MinValue;
        }

        public static bool AsBool(this object? obj, bool defaultVal = false)
        {
            if (obj is bool boolObj)
                return boolObj;
            else return obj is int intObj ? !(intObj == 0) : bool.TryParse(obj.AsString(), out var b) ? b : defaultVal;
        }

        public static short AsShort(this object? obj, short defaultVal = default)
        {
            return obj is short intObj ? intObj : short.TryParse(obj.AsString(), out var val) ? val : defaultVal;
        }

        public static float AsFloat(this object? obj, float defaultVal = default)
        {
            return obj is float intObj ? intObj : float.TryParse(obj.AsString(), out var val) ? val : defaultVal;
        }

        public static decimal AsDecimal(this object? obj, decimal defaultVal = default)
        {
            return obj is decimal decObj ? decObj : decimal.TryParse(obj.AsString(), out var dec) ? dec : defaultVal;
        }

        public static T[] AppendAll<T>(this T[] first, T[] second)
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

        public static string ToBase64(this object obj)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(obj.AsString()));
        }

        public static string FromBase64(this string str)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(str));
        }

        public static T? Deserialize<T>(this HttpResponseMessage msg)
        {
            var stream = msg.Content.ReadAsStream();
            return stream.Length > 0 ? JsonSerializer.Deserialize<T>(msg.Content.ReadAsStream(), _options) : default;
        }

        public static T? Deserialize<T>(this string? str)
        {
            return str is null ? default : JsonSerializer.Deserialize<T>(str, _options);
        }

        public static string Serialize(this object obj)
        {
            return JsonSerializer.Serialize(obj, _options);
        }

        public static IEnumerable<T1> SelectNotNull<T1>(this IEnumerable<T1?> source)
        {
            foreach (var item in source)
            {
                if (item != null)
                    yield return item;
            }
        }

        public static IEnumerable<T2> SelectNotNull<T1, T2>(this IEnumerable<T1> source, Func<T1, T2?> selector)
        {
            foreach (var item in source)
            {
                var result = selector(item);

                if (result != null)
                    yield return result;
            }
        }

        public static string Decapitalize(this string str)
        {
            return string.Concat(str[0].ToString().ToLower(), str.AsSpan(1));
        }

        public static string Capitalize(this string str)
        {
            return string.Concat(str[0].ToString().ToUpper(), str.AsSpan(1));
        }

        public static string SpaceWords(this string str)
        {
            return WordBreakRegex().Replace(str, "$1 $2");
        }

        public static string GetDetails(this Exception exception)
        {
            return $"{exception.GetType()}: {exception.Message}\nStack Trace: {exception.StackTrace}";
        }

        public static bool IsCoreAttribute(this PlayerAttribute att)
        {
            return ((EAttribute)att.AttributeId).IsCoreAttribute();
        }

        public static bool IsCoreAttribute(this EAttribute att)
        {
            return att is Strength or Endurance or Intellect or Agility or Dexterity or Luck;
        }

        [GeneratedRegex("([a-z])([A-Z])")]
        private static partial Regex WordBreakRegex();
    }
}