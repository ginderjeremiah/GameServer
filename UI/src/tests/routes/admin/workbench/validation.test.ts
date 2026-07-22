import { describe, it, expect } from 'vitest';
import {
	entityBlockingWarnings,
	entityWarnings,
	fieldWarn,
	sectionBlockingWarning,
	sectionWarnings
} from '../../../../routes/admin/workbench/validation';
import type { EntityConfig, FieldConfig, Identified } from '../../../../routes/admin/workbench/entities/types';

interface Thing extends Identified {
	id: number;
	name: string;
	icon: string;
	rows: number[];
}

const nameField: FieldConfig<Thing> = {
	key: 'name',
	label: 'Name',
	type: 'text',
	required: true,
	reqMsg: 'Missing name'
};
const iconField: FieldConfig<Thing> = { key: 'icon', label: 'Icon', type: 'text', required: true, reqMsg: 'No icon' };
const plainField: FieldConfig<Thing> = { key: 'name', label: 'Name', type: 'text' };

describe('fieldWarn', () => {
	it('returns null for non-required fields', () => {
		expect(fieldWarn(plainField, { id: 1, name: '', icon: '', rows: [] })).toBeNull();
	});

	it('returns the message when a required value is empty or whitespace', () => {
		expect(fieldWarn(nameField, { id: 1, name: '', icon: 'x', rows: [] })).toBe('Missing name');
		expect(fieldWarn(nameField, { id: 1, name: '   ', icon: 'x', rows: [] })).toBe('Missing name');
	});

	it('returns null when a required value is present', () => {
		expect(fieldWarn(nameField, { id: 1, name: 'Goblin', icon: 'x', rows: [] })).toBeNull();
	});
});

const entity = {
	sections: [
		{ key: 'identity', label: 'Identity', glyph: 'tag', kind: 'fields', fields: [nameField, iconField] },
		{
			key: 'rows',
			label: 'Rows',
			glyph: 'bars',
			kind: 'table',
			itemsKey: 'rows',
			warn: (t: Thing) => (t.rows.length ? null : 'No rows')
		}
	]
} as unknown as EntityConfig<Thing>;

describe('sectionWarnings', () => {
	it('collects each failing required field in a fields section', () => {
		const warns = sectionWarnings(entity.sections[0], { id: 1, name: '', icon: '', rows: [] });
		expect(warns).toEqual(['Missing name', 'No icon']);
	});

	it('uses the section warn predicate for non-field sections', () => {
		expect(sectionWarnings(entity.sections[1], { id: 1, name: 'x', icon: 'x', rows: [] })).toEqual(['No rows']);
		expect(sectionWarnings(entity.sections[1], { id: 1, name: 'x', icon: 'x', rows: [1] })).toEqual([]);
	});

	it('also surfaces a fields section warn (a cross-field rule), after its field warns', () => {
		const section = {
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			kind: 'fields',
			fields: [nameField],
			warn: (t: Thing) => (t.icon ? null : 'No icon set')
		} as unknown as EntityConfig<Thing>['sections'][number];
		// Both the failing required field and the section warn surface, field warns first.
		expect(sectionWarnings(section, { id: 1, name: '', icon: '', rows: [] })).toEqual(['Missing name', 'No icon set']);
		// Section warn alone when the required field passes.
		expect(sectionWarnings(section, { id: 1, name: 'Ok', icon: '', rows: [] })).toEqual(['No icon set']);
		// Neither when both pass.
		expect(sectionWarnings(section, { id: 1, name: 'Ok', icon: 'x', rows: [] })).toEqual([]);
	});

	it('passes the baseline through to the warn predicate', () => {
		const section = {
			key: 'rows',
			label: 'Rows',
			glyph: 'bars',
			kind: 'table',
			itemsKey: 'rows',
			// Mirrors zone.ts's isHome gap: the pending record alone can't tell whether this is a
			// same-save clear, only comparing against the persisted baseline can.
			warn: (t: Thing, baseline: Thing | undefined) =>
				!t.rows.length && baseline?.rows.length ? 'Cleared this save' : null
		} as unknown as EntityConfig<Thing>['sections'][number];
		const rec = { id: 1, name: 'x', icon: 'x', rows: [] };
		expect(sectionWarnings(section, rec)).toEqual([]);
		expect(sectionWarnings(section, rec, { id: 1, name: 'x', icon: 'x', rows: [] })).toEqual([]);
		expect(sectionWarnings(section, rec, { id: 1, name: 'x', icon: 'x', rows: [1] })).toEqual(['Cleared this save']);
	});
});

describe('entityWarnings', () => {
	it('aggregates warnings across every section', () => {
		expect(entityWarnings(entity, { id: 1, name: '', icon: '', rows: [] })).toEqual([
			'Missing name',
			'No icon',
			'No rows'
		]);
	});

	it('is empty when the record is complete', () => {
		expect(entityWarnings(entity, { id: 1, name: 'Goblin', icon: 'g.png', rows: [1] })).toEqual([]);
	});
});

describe('sectionBlockingWarning', () => {
	const blockingSection = {
		key: 'rows',
		label: 'Rows',
		glyph: 'bars',
		kind: 'table',
		itemsKey: 'rows',
		warn: (t: Thing) => (t.rows.length ? null : { message: 'No rows', blocking: true })
	} as unknown as EntityConfig<Thing>['sections'][number];

	const advisorySection = {
		key: 'rows',
		label: 'Rows',
		glyph: 'bars',
		kind: 'table',
		itemsKey: 'rows',
		warn: (t: Thing) => (t.rows.length ? null : 'No rows')
	} as unknown as EntityConfig<Thing>['sections'][number];

	it('returns the message when the warn is flagged blocking', () => {
		expect(sectionBlockingWarning(blockingSection, { id: 1, name: 'x', icon: 'x', rows: [] })).toBe('No rows');
	});

	it('returns null for a plain-string (advisory) warn', () => {
		expect(sectionBlockingWarning(advisorySection, { id: 1, name: 'x', icon: 'x', rows: [] })).toBeNull();
	});

	it('returns null once the condition clears', () => {
		expect(sectionBlockingWarning(blockingSection, { id: 1, name: 'x', icon: 'x', rows: [1] })).toBeNull();
	});

	it('a fields section warn can also be flagged blocking, independent of its required-field warnings', () => {
		const section = {
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			kind: 'fields',
			fields: [nameField],
			warn: (t: Thing) => (t.icon ? null : { message: 'No icon set', blocking: true })
		} as unknown as EntityConfig<Thing>['sections'][number];
		expect(sectionBlockingWarning(section, { id: 1, name: '', icon: '', rows: [] })).toBe('No icon set');
	});
});

describe('entityBlockingWarnings', () => {
	const mixedEntity = {
		sections: [
			{ key: 'identity', label: 'Identity', glyph: 'tag', kind: 'fields', fields: [nameField, iconField] },
			{
				key: 'rows',
				label: 'Rows',
				glyph: 'bars',
				kind: 'table',
				itemsKey: 'rows',
				warn: (t: Thing) => (t.rows.length ? null : { message: 'No rows', blocking: true })
			}
		]
	} as unknown as EntityConfig<Thing>;

	it('only collects the blocking warnings, skipping required-field (always advisory) warnings', () => {
		expect(entityBlockingWarnings(mixedEntity, { id: 1, name: '', icon: '', rows: [] })).toEqual(['No rows']);
	});

	it('is empty once every blocking condition clears, even with unrelated advisory warnings still present', () => {
		expect(entityBlockingWarnings(mixedEntity, { id: 1, name: '', icon: '', rows: [1] })).toEqual([]);
	});
});
