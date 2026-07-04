// @vitest-environment jsdom
import { describe, it, expect } from 'vitest';
import { tutorialAnchor, getTutorialAnchor } from '$components/tour/tutorial-anchor';

const makeNode = () => document.createElement('div');

describe('tutorialAnchor', () => {
	it('registers a node under its key and resolves it via getTutorialAnchor', () => {
		const node = makeNode();
		const action = tutorialAnchor(node, 'skill-bar');
		expect(getTutorialAnchor('skill-bar')).toBe(node);
		action.destroy();
	});

	it('returns undefined for an unregistered key', () => {
		expect(getTutorialAnchor('never-registered')).toBeUndefined();
	});

	it('unregisters on destroy', () => {
		const node = makeNode();
		const action = tutorialAnchor(node, 'help-button');
		action.destroy();
		expect(getTutorialAnchor('help-button')).toBeUndefined();
	});

	it('moves registration to the new key on update', () => {
		const node = makeNode();
		const action = tutorialAnchor(node, 'old-key');
		action.update('new-key');
		expect(getTutorialAnchor('old-key')).toBeUndefined();
		expect(getTutorialAnchor('new-key')).toBe(node);
		action.destroy();
	});

	it('update is a no-op when the key is unchanged', () => {
		const node = makeNode();
		const action = tutorialAnchor(node, 'same-key');
		action.update('same-key');
		expect(getTutorialAnchor('same-key')).toBe(node);
		action.destroy();
	});

	it('does not clobber a second element that already claimed the key on destroy of the first', () => {
		const first = makeNode();
		const second = makeNode();
		const firstAction = tutorialAnchor(first, 'shared-key');
		// Simulates an overlapping screen transition: a second mount claims the key before the first's
		// destroy runs.
		const secondAction = tutorialAnchor(second, 'shared-key');
		expect(getTutorialAnchor('shared-key')).toBe(second);

		firstAction.destroy();
		// The first mount's destroy must not delete the second mount's registration.
		expect(getTutorialAnchor('shared-key')).toBe(second);

		secondAction.destroy();
		expect(getTutorialAnchor('shared-key')).toBeUndefined();
	});
});
