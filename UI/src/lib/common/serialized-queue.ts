/**
 * Serializes async operations onto a single chain so each one runs only after the previous has fully
 * settled — its persist and any rollback included. Overlapping optimistic mutations would otherwise
 * interleave their rollback baselines: one operation's rollback could restore a snapshot taken before
 * another's optimistic change, silently discarding it and leaving local state diverged from the server.
 * Chaining queues the operations instead, which is better UX than dropping the later one — the same
 * "collapse concurrency" spirit as auth.ts's single-flight refresh.
 *
 * Each caller keeps its own optimistic-apply + rollback policy inside the operation closure; the queue
 * only owns the ordering and the always-fulfilled tail.
 */
export class SerializedQueue {
	/** Tail of the queue; each enqueued operation chains off it. Kept always-fulfilled (see {@link run}). */
	private tail: Promise<unknown> = Promise.resolve();

	/**
	 * Queues `operation` to run after every previously-enqueued operation has settled, returning a promise
	 * for its result (rejecting if `operation` itself rejects). `operation` runs regardless of whether the
	 * previous one fulfilled or rejected. The internal tail swallows that rejection so one operation's
	 * failure can't break the chain or surface as an unhandled rejection on the shared promise.
	 */
	run<T>(operation: () => Promise<T>): Promise<T> {
		const result = this.tail.then(operation, operation);
		this.tail = result.catch(() => undefined);
		return result;
	}
}
