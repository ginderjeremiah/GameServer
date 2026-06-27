import { describe, it, expect } from 'vitest';
import { entityWarnings, fieldWarn, sectionWarnings } from '../../../../routes/admin/workbench/validation';
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
