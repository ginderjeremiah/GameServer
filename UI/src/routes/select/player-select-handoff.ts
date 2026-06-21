/* One-shot handoff of the account's character list from the login page to the character-select
   screen. `Login` returns the summaries, and selecting a character happens on the separate `/select`
   route — but there is no "list my players" endpoint to re-fetch from (the list is only returned by
   the credentialed `Login`), so the login page stashes the summaries here and the select page takes
   them on mount. Deliberately non-reactive and read-once: a select page reached without a handoff
   (a refresh, or a direct deep-link) finds nothing and bounces back to login. */

import type { IPlayerSummary } from '$lib/api';

let summaries: IPlayerSummary[] | null = null;

export const playerSelectHandoff = {
	/** Stash the account's characters for the select screen to pick up. */
	set(list: IPlayerSummary[]): void {
		summaries = list;
	},

	/** Take (and clear) the stashed characters. Returns null when nothing was handed off. */
	take(): IPlayerSummary[] | null {
		const list = summaries;
		summaries = null;
		return list;
	}
};
