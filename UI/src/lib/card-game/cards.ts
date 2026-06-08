/* Card-Boss Duel ("The Initiative Loom") — the deck.

   These are the demo's hardcoded sample cards. When the minigame is wired to
   real character data, the deck will be derived from the player's skills and
   attributes (Strike = a basic attack coloured by max(STR,INT), Channel = a
   rare skill card, etc.) and these literals replaced by that mapping. */

/** Which lane/behaviour a card expresses. */
export type CardKind = 'block' | 'attack' | 'channel';

export interface CardDef {
	/** Stable key, also the deck-list token. */
	key: string;
	label: string;
	kind: CardKind;
	/** Block: ticks the defensive span covers. */
	span?: number;
	/** Attack/Channel: ticks of wind-up before the hit resolves. */
	windup?: number;
	/** Attack/Channel: damage dealt when the span's end crosses NOW. */
	dmg?: number;
	/** One-line stat text shown on the card face. */
	meta: string;
	/** Rare/skill card — rendered with the epic accent. */
	skill?: boolean;
}

export const CARDS: Record<string, CardDef> = {
	guard: { key: 'guard', label: 'Guard', kind: 'block', span: 7, meta: 'block · 7t' },
	dodge: { key: 'dodge', label: 'Dodge', kind: 'block', span: 3, meta: 'block · 3t' },
	slash: { key: 'slash', label: 'Slash', kind: 'attack', windup: 3, dmg: 16, meta: 'cast 3 → 16' },
	channel: { key: 'channel', label: 'Channel', kind: 'channel', windup: 9, dmg: 46, meta: 'cast 9 → 46', skill: true }
};

/** The demo's starting deck composition (shuffled on reset). */
export const STARTER_DECK: readonly string[] = [
	'guard',
	'guard',
	'guard',
	'guard',
	'dodge',
	'dodge',
	'slash',
	'slash',
	'slash',
	'slash',
	'slash',
	'channel',
	'channel',
	'dodge'
];

/** Duration in ticks a card occupies its lane (block span or attack/channel wind-up). */
export function cardDuration(card: CardDef): number {
	return card.kind === 'block' ? (card.span ?? 0) : (card.windup ?? 0);
}
