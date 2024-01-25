namespace GameLibrary
{
    public sealed class Mulberry32
    {
        private uint _seed;

        public Mulberry32(uint seed)
        {
            _seed = seed;
        }

        public double Next()
        {
            _seed += 0x6D2B79F5;
            var t = (_seed ^ (_seed >> 15)) * (1 | _seed);
            t = (t + ((t ^ (t >> 7)) * (61 | t))) ^ t;
            return (t ^ (t >> 14)) / (double)4294967296;
        }
    }

}
