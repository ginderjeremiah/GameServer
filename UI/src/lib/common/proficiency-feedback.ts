/* Player-facing phrasing for the `ProficiencyXpGained` push (spike #982 area H). A won battle accrues
   proficiency XP, which can cross a level, hit an authored milestone (optionally granting a skill), or
   open a brand-new proficiency. These pure formatters are the single home for that phrasing so the
   success-toasts and the combat-log lines stay consistent — mirroring `challengeCompletedMessage`. The
   caller resolves the proficiency/skill names from reference data and decides which channel each line
   goes to; these just shape the text. */

/** Combat-log line announcing the proficiency XP a battle earned. `xp` is the caller-rounded whole
 *  number of XP gained (the caller suppresses the line when it rounds to zero). */
export function proficiencyXpMessage(proficiencyName: string, xp: number): string {
	return `${proficiencyName}: +${xp} proficiency XP`;
}

/** Announces a proficiency reaching a new level (no milestone). Used for both the toast and the log. */
export function proficiencyLevelMessage(proficiencyName: string, level: number): string {
	return `${proficiencyName} reached level ${level}`;
}

/** Announces an authored milestone the gain crossed, naming the skill it granted when present (a
 *  milestone with no skill is a bonus-only milestone). Used for both the toast and the log. */
export function proficiencyMilestoneMessage(proficiencyName: string, level: number, skillName?: string): string {
	const base = `${proficiencyName} milestone reached: level ${level}`;
	return skillName ? `${base} — unlocked ${skillName}` : base;
}

/** Announces a newly-opened proficiency (a maxed tier's next tier within its path). Used for both the
 *  toast and the log. */
export function proficiencyOpenedMessage(proficiencyName: string): string {
	return `New proficiency unlocked: ${proficiencyName}`;
}
