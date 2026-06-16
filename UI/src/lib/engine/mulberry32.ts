export class Mulberry32 {
	#seed: number;

	constructor(seed: number) {
		this.#seed = seed;
	}

	next() {
		// Truncate back to a uint32 each step to match the C# port's native `uint` wrap-around.
		// Kept as an unbounded JS double, the accumulated seed loses its low bits once it exceeds
		// 2^53 (~4.9M draws), silently diverging the two streams; the `>>> 0` pins it to 32 bits.
		this.#seed = (this.#seed + 0x6d2b79f5) >>> 0;
		let t = Math.imul(this.#seed ^ (this.#seed >>> 15), 1 | this.#seed);
		t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
		return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
	}
}
