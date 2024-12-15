export class Mulberry32 {
	#seed: number;

	constructor(seed: number) {
		this.#seed = seed;
	}

	next() {
		this.#seed += 0x6d2b79f5;
		let t = Math.imul(this.#seed ^ (this.#seed >>> 15), 1 | this.#seed);
		t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
		return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
	}
}
