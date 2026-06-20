import { describe, it, expect } from 'vitest';
import type { LevelRange } from '$routes/game/screens/codex/enemy-level';
import {
	type EnemySearchSortFields,
	enemyAccent,
	enemyKindLabel,
	formatBand,
	formatCooldown,
	matchesEnemySearch,
	sortEnemyRows,
	tabAccent
} from '$routes/game/screens/codex/codex-display';

/* A search/sort row with sensible defaults so each test only states what matters. The `searchText`
   is the pre-lowercased haystack the view builds (name + kind + spawn zones). */
const row = (over: Partial<EnemySearchSortFields> & { name: string }): EnemySearchSortFields => ({
	level: 1,
	searchText: over.name.toLowerCase(),
	...over
});

describe('formatBand', () => {
	it('shows a fixed boss level as L{n}', () => {
		expect(formatBand({ min: 10, max: 10, fixed: true } as LevelRange)).toBe('L10');
	});

	it('shows a ranged band as min–max', () => {
		expect(formatBand({ min: 18, max: 28, fixed: false } as LevelRange)).toBe('18–28');
	});
});

describe('formatCooldown', () => {
	it('renders a utility (zero) cooldown as a dash', () => {
		expect(formatCooldown(0)).toBe('—');
	});

	it('renders whole-second cooldowns without a decimal', () => {
		expect(formatCooldown(2000)).toBe('2s');
		expect(formatCooldown(6000)).toBe('6s');
	});

	it('renders sub-second precision to one decimal', () => {
		expect(formatCooldown(1800)).toBe('1.8s');
		expect(formatCooldown(900)).toBe('0.9s');
	});
});

describe('accents + labels', () => {
	it('tints a boss gold and a normal enemy salmon', () => {
		expect(enemyAccent(true)).toBe('var(--boss-accent)');
		expect(enemyAccent(false)).toBe('var(--enemy-accent)');
	});

	it('labels the enemy kind', () => {
		expect(enemyKindLabel(true)).toBe('Boss');
		expect(enemyKindLabel(false)).toBe('Enemy');
	});

	it('maps each tab to its themed section accent', () => {
		expect(tabAccent('enemies')).toBe('var(--enemy-accent)');
		expect(tabAccent('zones')).toBe('var(--accent)');
		expect(tabAccent('skills')).toBe('var(--attr-intellect)');
	});
});

describe('matchesEnemySearch', () => {
	const dustSkitterer = row({ name: 'Dust Skitterer', searchText: 'dust skitterer enemy emberreach ashfen marsh' });

	it('matches everything for an empty or whitespace-only query', () => {
		expect(matchesEnemySearch(dustSkitterer, '')).toBe(true);
		expect(matchesEnemySearch(dustSkitterer, '   ')).toBe(true);
	});

	it('matches a substring of the name, case-insensitively', () => {
		expect(matchesEnemySearch(dustSkitterer, 'dust')).toBe(true);
		expect(matchesEnemySearch(dustSkitterer, 'SKITTER')).toBe(true);
		expect(matchesEnemySearch(dustSkitterer, '  Dust  ')).toBe(true); // trims surrounding whitespace
	});

	it('matches the enemy kind and the zones it appears in', () => {
		expect(matchesEnemySearch(dustSkitterer, 'enemy')).toBe(true);
		expect(matchesEnemySearch(dustSkitterer, 'emberreach')).toBe(true);
		expect(matchesEnemySearch(dustSkitterer, 'marsh')).toBe(true);
	});

	it('returns false when the query matches nothing', () => {
		expect(matchesEnemySearch(dustSkitterer, 'dragon')).toBe(false);
	});
});

describe('sortEnemyRows', () => {
	const rows = [
		row({ name: 'Cinder Tyrant', level: 10 }),
		row({ name: 'Dust Skitterer', level: 1 }),
		row({ name: 'Bog Lurker', level: 11 })
	];

	it('sorts by ascending level, then name on ties', () => {
		const ordered = [...rows].sort(sortEnemyRows('level')).map((r) => r.name);
		expect(ordered).toEqual(['Dust Skitterer', 'Cinder Tyrant', 'Bog Lurker']);
	});

	it('breaks a level tie alphabetically by name', () => {
		const tied = [row({ name: 'Wraith', level: 5 }), row({ name: 'Aphid', level: 5 })];
		expect([...tied].sort(sortEnemyRows('level')).map((r) => r.name)).toEqual(['Aphid', 'Wraith']);
	});

	it('sorts alphabetically by name', () => {
		const ordered = [...rows].sort(sortEnemyRows('name')).map((r) => r.name);
		expect(ordered).toEqual(['Bog Lurker', 'Cinder Tyrant', 'Dust Skitterer']);
	});
});
