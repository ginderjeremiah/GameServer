import { describe, it, expect } from 'vitest';
import {
	proficiencyXpMessage,
	proficiencyLevelMessage,
	proficiencyMilestoneMessage,
	proficiencyOpenedMessage
} from '$lib/common';

describe('proficiency-feedback messages', () => {
	it('formats an XP-gained line', () => {
		expect(proficiencyXpMessage('Fire Magic', 12)).toBe('Fire Magic: +12 proficiency XP');
	});

	it('formats a level-up line', () => {
		expect(proficiencyLevelMessage('Fire Magic', 5)).toBe('Fire Magic reached level 5');
	});

	it('names the granted skill in a milestone message', () => {
		expect(proficiencyMilestoneMessage('Fire Magic', 5, 'Fireball')).toBe(
			'Fire Magic milestone reached: level 5 — unlocked Fireball'
		);
	});

	it('omits the skill clause for a bonus-only milestone', () => {
		expect(proficiencyMilestoneMessage('Fire Magic', 10)).toBe('Fire Magic milestone reached: level 10');
	});

	it('announces a newly-opened proficiency', () => {
		expect(proficiencyOpenedMessage('Inferno Magic')).toBe('New proficiency unlocked: Inferno Magic');
	});
});
