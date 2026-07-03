/* Player challenge-completion store — which challenges the player has completed
   (`GetPlayerChallenges` socket command), shared across screens so the source is fetched once
   and stays consistent. The Challenges screen renders the full progress breakdown from it; the
   Fight screen reads completion to gate zone navigation (a zone unlocks once its gating
   challenge is completed). */

import { fetchSocketData, type IPlayerChallenge } from '$lib/api';
import { CoalescedLoader } from '$lib/common/coalesced-loader';

let challenges = $state<IPlayerChallenge[]>([]);
let loaded = $state(false);
let error = $state(false);

const fetchChallenges = async () => {
	try {
		// fetchSocketData throws on a socket error, preserving this try/catch contract.
		challenges = (await fetchSocketData('GetPlayerChallenges')) ?? [];
		error = false;
		loaded = true;
	} catch {
		// A failed load is not a genuine empty result; leave zones gated as-is and let the next
		// load retry. The Challenges screen surfaces the error to the user.
		error = true;
	}
};

/** Coalesces concurrent callers (game boot + screen mount) onto one request; a forced load
 *  issued mid-flight chains a fresh fetch so it never resolves with stale data. */
const loader = new CoalescedLoader(fetchChallenges, () => loaded);

export const playerChallenges = {
	get all() {
		return challenges;
	},
	get loaded() {
		return loaded;
	},
	get error() {
		return error;
	},

	/** Fetch the player's challenge progress. Idempotent — only hits the network the first
	 *  time unless `force` re-fetches the latest values (e.g. on the challenges screen). */
	async load(force = false) {
		await loader.load(force);
	},

	/** Whether the player has completed the given challenge. */
	isChallengeCompleted(challengeId: number) {
		return challenges.some((c) => c.challengeId === challengeId && c.completed);
	},

	/** Mark a challenge completed locally (e.g. on the `ChallengeCompleted` push) so completion-gated
	 *  UI — zone navigation — reflects it immediately without re-fetching. A later `load(force)` will
	 *  reconcile against the authoritative server progress. */
	markCompleted(challengeId: number) {
		const existing = challenges.find((c) => c.challengeId === challengeId);
		if (existing?.completed) {
			return;
		}
		if (existing) {
			challenges = challenges.map((c) => (c.challengeId === challengeId ? { ...c, completed: true } : c));
		} else {
			challenges = [...challenges, { challengeId, progress: 0, completed: true }];
		}
	},

	/** Reset to the unloaded state (e.g. on logout / session replacement). */
	reset() {
		challenges = [];
		loaded = false;
		error = false;
		loader.reset();
	}
};
