let total = $state(0);

/**
 * Cross-surface unsaved-change count for the admin Workbench. Only one editing surface (a
 * single-entity Workbench panel, or the bespoke Progression editor) is ever mounted at a time, so
 * each one reports its own pending-change count here rather than the admin shell reaching into
 * per-surface stores it otherwise has no reason to know about. Read by the shell to confirm before
 * a tool switch or leaving `/admin` discards unsaved work.
 */
export const workbenchDirty = {
	get total(): number {
		return total;
	},
	set(value: number) {
		total = value;
	}
};
