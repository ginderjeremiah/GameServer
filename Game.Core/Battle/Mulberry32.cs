namespace Game.Core.Battle
{
    /// <summary>
    /// A simple random number generator.
    /// </summary>
    public sealed class Mulberry32
    {
        private uint _seed;

        /// <summary>
        /// Creates a new instance of <see cref="Mulberry32"/> with the given <paramref name="seed"/>.
        /// </summary>
        /// <param name="seed"></param>
        public Mulberry32(uint seed)
        {
            _seed = seed;
        }

        /// <summary>
        /// Generates a random number between 0 and 1.0.
        /// </summary>
        /// <returns></returns>
        public double Next()
        {
            _seed += 0x6D2B79F5;
            var t = (_seed ^ (_seed >> 15)) * (1 | _seed);
            t = (t + ((t ^ (t >> 7)) * (61 | t))) ^ t;
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }
}
