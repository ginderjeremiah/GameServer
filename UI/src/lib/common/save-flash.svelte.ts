/** Brief "saved" confirmation flash with a self-owned timer lifecycle — the pattern duplicated
 *  across the Attributes/Options draft-edit view-models before being extracted here. A consumer
 *  calls {@link flash} after a successful save and must call {@link dispose} on teardown to clear
 *  a pending timer. */
export class SaveFlash {
	active = $state(false);

	#timer: ReturnType<typeof setTimeout> | undefined;

	constructor(private readonly durationMs = 1900) {}

	flash(): void {
		this.active = true;
		if (this.#timer) {
			clearTimeout(this.#timer);
		}
		this.#timer = setTimeout(() => {
			this.active = false;
		}, this.durationMs);
	}

	reset(): void {
		this.active = false;
	}

	dispose(): void {
		if (this.#timer) {
			clearTimeout(this.#timer);
		}
	}
}
