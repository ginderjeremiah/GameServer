import { EActivityKey } from '$lib/api';

/*
 * Friendly display labels for `EActivityKey` — the training hooks proficiency paths bind to. Shared by
 * the Workbench progression editor (path picker/list) and the player-facing Proficiencies "Trained by"
 * chip so every surface spells an activity key identically (Dot → "DoT", Crit → "Critical damage").
 */

/** How an activity key trains: typed damage dealt, a combat event, or typed damage taken (resist). */
export type ActivityKeyKind = 'offense' | 'event' | 'resist';

/** An activity key's classification plus its bare (unsuffixed) display label. */
export interface ActivityKeyDisplay {
	kind: ActivityKeyKind;
	label: string;
}

/** Combat-event activity keys (not damage types) — labelled by the quantity they train on. */
const ACTIVITY_EVENT_LABELS: Partial<Record<EActivityKey, string>> = {
	[EActivityKey.Crit]: 'Critical damage',
	[EActivityKey.Dodge]: 'Dodged damage',
	[EActivityKey.Heal]: 'Healing done',
	[EActivityKey.Reflect]: 'Reflected damage',
	[EActivityKey.Hex]: 'Vulnerability damage enabled',
	[EActivityKey.Momentum]: 'Ramp damage enabled',
	[EActivityKey.Sunder]: 'Mitigation damage enabled',
	[EActivityKey.Cull]: 'Execute damage enabled',
	[EActivityKey.Parry]: 'Counter damage',
	[EActivityKey.Cadence]: 'Cadence damage enabled'
};

/** Spell the damage-type stem of an activity-key name ("Dot" → "DoT"; others read as authored). */
const typeStemLabel = (stem: string): string => (stem === 'Dot' ? 'DoT' : stem);

/**
 * Classifies an activity key and gives its bare label: a combat event reads as the quantity it trains
 * ("Critical damage"), otherwise the damage-type stem — with `kind` telling the caller whether the stem
 * belongs to the offense (damage dealt) or resist (damage taken) book.
 */
export const activityKeyDisplay = (key: EActivityKey): ActivityKeyDisplay => {
	const event = ACTIVITY_EVENT_LABELS[key];
	if (event !== undefined) {
		return { kind: 'event', label: event };
	}
	const name = EActivityKey[key];
	if (name.endsWith('Resist')) {
		return { kind: 'resist', label: typeStemLabel(name.slice(0, -'Resist'.length)) };
	}
	return { kind: 'offense', label: typeStemLabel(name) };
};

/**
 * An activity key as a standalone friendly label: a combat event by what it trains ("Critical damage"),
 * an incoming-book key suffixed "(resist)", or the bare damage-type stem for the output book.
 */
export const activityKeyLabel = (key: EActivityKey): string => {
	const { kind, label } = activityKeyDisplay(key);
	return kind === 'resist' ? `${label} (resist)` : label;
};
