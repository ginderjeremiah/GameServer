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
    /// <see cref="EvaluateAsync"/> is the only method that trusts the server-side script cache: the first call for
    /// a given script sends the full text (an <c>EVAL</c>, which as a side effect caches it under its hash), every
    /// call after that sends just the hash, and a <c>NOSCRIPT</c> reply (a manual <c>SCRIPT FLUSH</c>, or a Redis
    /// restart/failover that lost its script cache) falls back to a full-text resend — so an awaited caller
    /// self-heals from a cleared cache no matter the cause.
    /// </para>
    /// <para>
    /// <see cref="Evaluate"/> (the fire-and-forget-capable sync path) passes <see cref="CommandFlags.NoScriptCache"/>
    /// on every call (#2126). This isn't just skipping our own <c>_hash</c>/<c>_warmed</c> bookkeeping — without
    /// it, StackExchange.Redis' own <c>ScriptEvalMessage</c> transparently substitutes <c>EVALSHA</c> for any
    /// script text it has seen succeed before (a *per-connection* cache, independent of this class), and its
    /// <c>NOSCRIPT</c> self-heal only runs inside the reply processor — which a <see cref="CommandFlags.FireAndForget"/>
    /// command never reaches, since the reply is discarded before it gets there. So once that library-level cache
    /// believes the script is known, a fire-and-forget call silently wedges into <c>NOSCRIPT</c> for the rest of
    /// the process the moment the server-side cache is cleared (restart, failover, <c>SCRIPT FLUSH</c>) — and
    /// <c>NoScriptCache</c> is the only way to opt a call out of that library behaviour, forcing a literal
    /// <c>EVAL</c> every time. These scripts are ~100-300 bytes, so the bytes an <c>EVALSHA</c> would save isn't
    /// worth that failure mode.
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
            // NoScriptCache forces a literal EVAL and opts out of StackExchange.Redis' own transparent
            // EVALSHA-once-known cache — see the class doc for why this path can't safely trust either that or
            // our own _warmed the way EvaluateAsync does.
            return db.ScriptEvaluate(_script, keys, values, flags | CommandFlags.NoScriptCache);
        }
    }
}
