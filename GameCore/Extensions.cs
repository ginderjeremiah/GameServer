using GameCore.Cache;
using GameCore.Database;
using GameCore.Logging;
using GameCore.Logging.Interfaces;
using GameCore.PubSub;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Data;
using System.Text;
using System.Text.Json;

namespace GameCore
{
    public static class Extensions
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
            if (obj is int intObj)
                return intObj;
            else if (int.TryParse(obj.AsString(), out var val))
                return val;
            else
                return defaultVal;
        }

        public static DateTime AsDate(this object? obj, DateTime? defaultVal = null)
        {
            if (obj is DateTime dtObj)
                return dtObj;
            else if (DateTime.TryParse(obj.AsString(), out var dt))
                return dt;
            else
                return defaultVal ?? DateTime.MinValue;
        }

        public static bool AsBool(this object? obj, bool defaultVal = false)
        {
            if (obj is bool boolObj)
                return boolObj;
            else if (obj is int intObj)
                return !(intObj == 0);
            else if (bool.TryParse(obj.AsString(), out var b))
                return b;
            else
                return defaultVal;
        }

        public static short AsShort(this object? obj, short defaultVal = default)
        {
            if (obj is short intObj)
                return intObj;
            else if (short.TryParse(obj.AsString(), out var val))
                return val;
            else
                return defaultVal;
        }

        public static float AsFloat(this object? obj, float defaultVal = default)
        {
            if (obj is float intObj)
                return intObj;
            else if (float.TryParse(obj.AsString(), out var val))
                return val;
            else
                return defaultVal;
        }

        public static decimal AsDecimal(this object? obj, decimal defaultVal = default)
        {
            if (obj is decimal decObj)
                return decObj;
            else if (decimal.TryParse(obj.AsString(), out var dec))
                return dec;
            else
                return defaultVal;
        }

        public static T[] AppendAll<T>(this T[] first, T[] second)
        {
            T[] output = new T[first.Length + second.Length];
            for (int i = 0; i < first.Length; i++)
                output[i] = first[i];
            for (int i = 0; i < second.Length; i++)
                output[first.Length + i] = second[i];
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
            return JsonSerializer.Deserialize<T>(msg.Content.ReadAsStream(), _options);
        }

        public static T? Deserialize<T>(this string? str)
        {
            if (str == null)
            {
                return default;
            }
            else
            {
                return JsonSerializer.Deserialize<T>(str, _options);
            }
        }

        public static T? Deserialize<T>(this RedisValue val)
        {
            var value = (string?)val;
            if (value is null)
            {
                return default;
            }
            else
            {
                return JsonSerializer.Deserialize<T>(value, _options);
            }
        }

        public static string Serialize<T>(this T obj)
        {
            return JsonSerializer.Serialize(obj, _options);
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

        public static string ToCamelCase(this string str)
        {
            return string.Concat(str[0].ToString().ToLower(), str.AsSpan(1));
        }

        public static string GetDetails(this Exception exception)
        {
            return $"{exception.GetType()}: {exception.Message}\nStack Trace: {exception.StackTrace}";
        }

        public static void AddDataProvider(this IServiceCollection services)
        {
            DataProviderFactory.AddDataProviderService(services);
        }

        public static void AddCacheProvider(this IServiceCollection services)
        {
            CacheProviderFactory.AddCacheProviderService(services);
        }

        public static void AddPubSubProvider(this IServiceCollection services)
        {
            PubSubProviderFactory.AddPubSubProviderService(services);
        }

        public static void AddApiLogging(this IServiceCollection services)
        {
            services.AddTransient<IApiLogger, ApiLogger>();
        }
    }
}