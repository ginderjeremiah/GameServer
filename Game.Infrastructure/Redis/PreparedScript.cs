using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;

namespace Game.Infrastructure.Redis
{
    /// <summary>
    /// Wraps a Lua script's source text with its SHA1 hash so repeat calls send the 20-byte <c>EVALSHA</c> hash
    /// instead of retransmitting the full script body on every hot-path call (#2056). The scripts stay raw
    /// <c>KEYS[n]</c>/<c>ARGV[n]</c> text (some build a variable-length <c>ARGV</c>, which <c>LuaScript.Prepare</c>'s
    /// fixed named-parameter model can't express), so this talks to <see cref="IDatabase"/> directly rather than
    /// through that helper.
    /// <para>
    /// The very first call this process makes for a given script sends the full text — an <c>EVAL</c>, which as a
    /// side effect caches the script on the server under its hash — so a cold script cache never causes a
    /// <c>NOSCRIPT</c> failure; every call after that sends the hash. <see cref="EvaluateAsync"/> additionally
    /// falls back to a full-text resend if the server reports <c>NOSCRIPT</c> after the script was already warmed
    /// (a manual <c>SCRIPT FLUSH</c> or a Redis restart that lost its script cache), so an awaited caller
    /// self-heals. <see cref="Evaluate"/> (fire-and-forget) has no response to inspect for that — the same
    /// no-delivery-guarantee every other <see cref="CommandFlags.FireAndForget"/> call in this tier already
    /// accepts.
    /// </para>
    /// <para>
    /// <see cref="Cache.Redis.RedisService"/>/<see cref="PubSub.Redis.RedisQueue"/> are resolved transient
    /// (constructed per DI scope), so each script is prepared once as a <c>static readonly</c> field on its
    /// owning class — the hash and warm state are shared process-wide rather than recomputed or re-sent per
    /// instance.
    /// </para>
    /// </summary>
    internal sealed class PreparedScript
    {
        private readonly string _script;
        private readonly byte[] _hash;
        private volatile bool _warmed;

        public PreparedScript(string script)
        {
            _script = script;
            _hash = SHA1.HashData(Encoding.UTF8.GetBytes(script));
        }

        // Test-only seams (mirroring RedisMultiplexerFactory.ResetForTesting): CreateAlreadyWarmedForTesting lets
        // a test force the hash-only path on a script Redis has never actually loaded, to pin the NOSCRIPT
        // self-heal without a process-wide SCRIPT FLUSH that would disturb every other script this process
        // already warmed; IsWarmedForTesting exposes the state a test needs to assert on since a failed call
        // can't be told apart from a successful one by exception shape alone.
        internal static PreparedScript CreateAlreadyWarmedForTesting(string script)
        {
            var prepared = new PreparedScript(script)
            {
                _warmed = true
            };
            return prepared;
        }

        internal bool IsWarmedForTesting => _warmed;

        public async Task<RedisResult> EvaluateAsync(IDatabaseAsync db, RedisKey[] keys, RedisValue[] values, CommandFlags flags = CommandFlags.None)
        {
            if (!_warmed)
            {
                var coldResult = await db.ScriptEvaluateAsync(_script, keys, values, flags);
                _warmed = true;
                return coldResult;
            }

            try
            {
                return await db.ScriptEvaluateAsync(_hash, keys, values, flags);
            }
            catch (RedisServerException ex) when (ex.Message.StartsWith("NOSCRIPT", StringComparison.Ordinal))
            {
                return await db.ScriptEvaluateAsync(_script, keys, values, flags);
            }
        }

        public RedisResult Evaluate(IDatabase db, RedisKey[] keys, RedisValue[] values, CommandFlags flags = CommandFlags.None)
        {
            if (!_warmed)
            {
                var coldResult = db.ScriptEvaluate(_script, keys, values, flags);
                _warmed = true;
                return coldResult;
            }

            return db.ScriptEvaluate(_hash, keys, values, flags);
        }
    }
}
