/* Client-side character-name validation, mirroring the backend `PlayerName` value object
   (Game.Core/Players/PlayerName.cs): a trimmed length in [MIN_NAME, MAX_NAME] with no control
   characters. The backend re-validates as anti-cheat, so this is purely for fast inline feedback —
   it must stay in step with the domain rule, not become a second source of truth. */

/** The shortest a (trimmed) character name may be — mirrors the backend `PlayerName.MinLength`. */
export const MIN_NAME = 1;
/** The longest a (trimmed) character name may be — mirrors the backend `PlayerName.MaxLength`. */
export const MAX_NAME = 20;

/** Matches .NET's `char.IsControl`: the C0 (0x00–0x1F) and C1 (0x7F–0x9F) control ranges. */
function hasControlChar(value: string): boolean {
	for (const ch of value) {
		const code = ch.codePointAt(0) ?? 0;
		if (code <= 0x1f || (code >= 0x7f && code <= 0x9f)) {
			return true;
		}
	}
	return false;
}

export interface NameValidation {
	/** Whether the trimmed name is acceptable. */
	ok: boolean;
	/** The trimmed name to submit (only meaningful when `ok`). */
	name: string;
	/** A human-readable reason when invalid, empty when valid. */
	msg: string;
}

/**
 * Validates and normalizes a player-supplied character name: trims surrounding whitespace, then
 * accepts it only when the trimmed value is within [{@link MIN_NAME}, {@link MAX_NAME}] and free of
 * control characters. Returns the normalized name to submit when valid.
 */
export function validatePlayerName(raw: string): NameValidation {
	const name = raw.trim();
	if (name.length < MIN_NAME) {
		return { ok: false, name: '', msg: 'Enter a character name.' };
	}
	if (name.length > MAX_NAME) {
		return { ok: false, name, msg: `Names must be ${MAX_NAME} characters or fewer.` };
	}
	if (hasControlChar(name)) {
		return { ok: false, name, msg: 'Names cannot contain control characters.' };
	}
	return { ok: true, name, msg: '' };
}
