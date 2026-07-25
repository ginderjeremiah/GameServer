import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent, waitFor } from '@testing-library/svelte';
import { EAttribute, EModifierType, ESkillAcquisition } from '$lib/api';

// `vi.mock` is hoisted within this file, so the factory resolves the shared stub by dynamic import
// rather than closing over the static one below.
vi.mock('$stores', async () => (await import('./progression-test-utils')).stubStores());

import TierDetail from '$routes/admin/workbench/progression/TierDetail.svelte';
import type { WorkbenchProficiency } from '$routes/admin/workbench/progression/types';
import {
	dangerModal,
	makeTierStore as makeStore,
	resetStores,
	staticData,
	tier as baseTier
} from './progression-test-utils';

// Tiers here carry id 5 and populated conlang fields: the retire/tab assertions below target id 5,
// and ConlangIdentity's decipher preview reads the authored word/pronunciation/translation.
const tier = (over: Partial<WorkbenchProficiency> = {}): WorkbenchProficiency =>
	baseTier({
		id: 5,
		iconPath: 'i.png',
		word: 'sijren',
		pronunciation: 'sij-ren',
		translation: 'The Riven Frost',
		...over
	});

beforeEach(resetStores);
afterEach(cleanup);

describe('TierDetail', () => {
	it('renders the tier name and path/ordinal headline', () => {
		const store = makeStore(tier());
		render(TierDetail, { props: { store } });

		expect(screen.getByRole('heading', { level: 2 }).textContent).toBe('Blades');
		expect(screen.getByText(/Fire Path · Tier 0/)).toBeTruthy();
	});

	it('switches tabs through the store', async () => {
		const store = makeStore(tier());
		render(TierDetail, { props: { store } });

		await fireEvent.click(screen.getByText('XP Curve'));
		expect(store.setTierTab).toHaveBeenCalledWith('xp');
	});

	it('navigates back to the path via the breadcrumb', async () => {
		const store = makeStore(tier());
		render(TierDetail, { props: { store } });

		await fireEvent.click(screen.getByRole('button', { name: 'Fire Path' }));
		expect(store.back).toHaveBeenCalledOnce();
	});

	// `description` round-tripped through profIdentityDto and was bounded in EF, but had no editor input
	// and so could never be authored (#2377) — unlike `designerNotes`, it ships unredacted to every client.
	it('authors the tier description through the store', async () => {
		const edited = tier();
		const store = makeStore(edited);
		render(TierDetail, { props: { store } });

		await fireEvent.input(screen.getByLabelText('Description'), { target: { value: 'A riven-frost blade art.' } });

		expect(store.patchProf).toHaveBeenCalledWith(5, expect.any(Function));
		const patch = vi.mocked(store.patchProf).mock.calls[0][1];
		patch(edited);
		expect(edited.description).toBe('A riven-frost blade art.');
	});
});

describe('TierDetail — retire confirm dialog', () => {
	it('retires immediately when nothing references the tier', async () => {
		const store = makeStore(tier());
		render(TierDetail, { props: { store } });

		await fireEvent.click(screen.getByText('Retire'));

		expect(dangerModal).not.toHaveBeenCalled();
		expect(store.retireProf).toHaveBeenCalledWith(5, true);
	});

	it('prompts before retiring a tier gating an item, and retires on confirm', async () => {
		dangerModal.mockResolvedValue(true);
		staticData.items = [{ id: 0, name: 'Iron Helm', requiredProficiencyId: 5 }];

		const store = makeStore(tier());
		render(TierDetail, { props: { store } });

		await fireEvent.click(screen.getByText('Retire'));

		expect(dangerModal).toHaveBeenCalledOnce();
		const body = dangerModal.mock.calls[0][0].body as string;
		expect(body).toContain('Iron Helm');
		await waitFor(() => expect(store.retireProf).toHaveBeenCalledWith(5, true));
	});

	it('does not retire when the confirm dialog is cancelled', async () => {
		dangerModal.mockResolvedValue(false);
		staticData.items = [{ id: 0, name: 'Iron Helm', requiredProficiencyId: 5 }];

		const store = makeStore(tier());
		render(TierDetail, { props: { store } });

		await fireEvent.click(screen.getByText('Retire'));

		expect(dangerModal).toHaveBeenCalledOnce();
		expect(store.retireProf).not.toHaveBeenCalled();
	});

	it('offers Reinstate for an already-retired tier', async () => {
		const store = makeStore(tier(), { isRetired: vi.fn(() => true) });
		render(TierDetail, { props: { store } });

		expect(screen.queryByText('Retire')).toBeNull();
		await fireEvent.click(screen.getByText('Reinstate'));
		expect(store.retireProf).toHaveBeenCalledWith(5, false);
	});

	it('sources the prerequisite reference from the live store, not the stale staticData snapshot (#1863)', async () => {
		dangerModal.mockResolvedValue(true);
		// staticData.proficiencies stays empty (last-saved state) — the gating edge only exists in
		// this session's unsaved edits, held on the store's live `profs`.
		const gatingTier = tier({ id: 6, name: 'Advanced Blades', prerequisiteIds: [5] });
		const store = makeStore(tier(), { profs: [tier(), gatingTier] });
		render(TierDetail, { props: { store } });

		await fireEvent.click(screen.getByText('Retire'));

		expect(dangerModal).toHaveBeenCalledOnce();
		const body = dangerModal.mock.calls[0][0].body as string;
		expect(body).toContain('Advanced Blades');
		await waitFor(() => expect(store.retireProf).toHaveBeenCalledWith(5, true));
	});
});

describe('TierDetail — tab bodies', () => {
	it('renders the XP curve tab', () => {
		const store = makeStore(tier(), { tierTab: 'xp' });
		render(TierDetail, { props: { store } });

		expect(screen.getByText('Max level')).toBeTruthy();
		expect(screen.getByText(/Derived per-level cost/)).toBeTruthy();
	});

	it('renders the milestones tab and adds a payout at the selected level', async () => {
		const store = makeStore(tier(), { tierTab: 'milestones' });
		render(TierDetail, { props: { store } });

		await fireEvent.click(screen.getByTestId('progression-add-payout'));
		expect(store.addPayout).toHaveBeenCalledWith(5, 1);
	});

	it('renders an existing payout at the selected level and edits/removes it', async () => {
		const payoutTier = tier({
			levelModifiers: [
				{ level: 1, attributeId: EAttribute.Strength, modifierTypeId: EModifierType.Additive, amount: 5 }
			],
			levelRewards: [{ level: 1, rewardSkillId: 2 }]
		});
		const store = makeStore(payoutTier, { tierTab: 'milestones' });
		render(TierDetail, { props: { store } });

		expect(screen.getByText('Milestone')).toBeTruthy();
		await fireEvent.click(screen.getByLabelText('Remove modifier'));
		expect(store.removeModifier).toHaveBeenCalledWith(5, 0);

		await fireEvent.click(screen.getByText(/Remove this payout level/));
		expect(store.removePayout).toHaveBeenCalledWith(5, 1);

		await fireEvent.click(screen.getByText('+ Add modifier'));
		expect(store.addModifier).toHaveBeenCalledWith(5, 1);
	});

	it('renders the gateways tab for a root tier with no prerequisites', () => {
		const store = makeStore(tier({ pathOrdinal: 0, prerequisiteIds: [] }), { tierTab: 'gateways' });
		render(TierDetail, { props: { store } });

		expect(screen.getByText(/Prerequisite proficiencies/)).toBeTruthy();
		expect(screen.getByText(/Starter tier/)).toBeTruthy();
	});

	it('disables the Gateways tab for a non-root tier with no prerequisites', () => {
		const store = makeStore(tier({ pathOrdinal: 1, prerequisiteIds: [] }), { tierTab: 'milestones' });
		render(TierDetail, { props: { store } });

		const tab = screen.getByText('Gateways').closest('button');
		expect(tab?.hasAttribute('disabled')).toBe(true);
	});

	it('keeps the Gateways tab enabled for a non-root tier that still carries prerequisites (e.g. reordered off root)', async () => {
		const store = makeStore(tier({ pathOrdinal: 1, prerequisiteIds: [3] }), { tierTab: 'milestones' });
		render(TierDetail, { props: { store } });

		const tab = screen.getByText('Gateways').closest('button');
		expect(tab?.hasAttribute('disabled')).toBe(false);

		await fireEvent.click(tab!);
		expect(store.setTierTab).toHaveBeenCalledWith('gateways');
	});

	it('warns the Gateways tab when a tier reordered off root still carries prerequisites (#2275)', () => {
		const stranded = makeStore(tier({ pathOrdinal: 1, prerequisiteIds: [3] }), { tierTab: 'milestones' });
		render(TierDetail, { props: { store: stranded } });
		const strandedTab = screen.getByText('Gateways').closest('button');
		expect(strandedTab?.querySelector('svg')).toBeTruthy();
		cleanup();

		const rootGateway = makeStore(tier({ pathOrdinal: 0, prerequisiteIds: [3] }), { tierTab: 'milestones' });
		render(TierDetail, { props: { store: rootGateway } });
		const rootTab = screen.getByText('Gateways').closest('button');
		expect(rootTab?.querySelector('svg')).toBeNull();
	});

	it('warns the Milestones tab when a reward skill lost its Player flag (#2333)', () => {
		staticData.skills = [{ id: 0, name: 'Roar', acquisition: ESkillAcquisition.Enemy }]; // flag stripped after reward

		const flagLost = makeStore(tier({ maxLevel: 5, levelRewards: [{ level: 3, rewardSkillId: 0 }] }));
		render(TierDetail, { props: { store: flagLost } });
		const flagLostTab = screen.getByText('Milestones').closest('button');
		expect(flagLostTab?.querySelector('svg')).toBeTruthy();
		cleanup();

		staticData.skills = [{ id: 0, name: 'Roar', acquisition: ESkillAcquisition.Player }];
		const stillFlagged = makeStore(tier({ maxLevel: 5, levelRewards: [{ level: 3, rewardSkillId: 0 }] }));
		render(TierDetail, { props: { store: stillFlagged } });
		const stillFlaggedTab = screen.getByText('Milestones').closest('button');
		expect(stillFlaggedTab?.querySelector('svg')).toBeNull();
	});

	it('lists an existing prerequisite chip, removes it, and adds a new one', async () => {
		// Gateways are cross-path only (#2128), so the prerequisite candidates live on a different
		// path than the gated tier's own (pathId 0, the tier() default).
		const basics = tier({ id: 3, pathId: 1, name: 'Basics', prerequisiteIds: [] });
		const advanced = tier({ id: 7, pathId: 1, name: 'Advanced', prerequisiteIds: [] });
		const gatedTier = tier({ prerequisiteIds: [3] });
		const store = makeStore(gatedTier, { profs: [gatedTier, basics, advanced], tierTab: 'gateways' });
		render(TierDetail, { props: { store } });

		expect(screen.getByText('Basics')).toBeTruthy();
		await fireEvent.click(screen.getByLabelText('Remove prerequisite'));
		expect(store.removePrerequisite).toHaveBeenCalledWith(5, 3);

		await fireEvent.change(screen.getByLabelText('Add prerequisite'), { target: { value: '7' } });
		expect(store.addPrerequisite).toHaveBeenCalledWith(5, 7);
	});

	it('offers an unsaved tier (negative id, never in staticData) as a prerequisite option (#1997)', async () => {
		// id -1 is the first id ProgressionStore.nextId hands out to a brand-new tier — deliberately
		// chosen here to also pin that it doesn't collide with the picker's own placeholder value.
		// Cross-path (#2128) so it isn't excluded as a same-path candidate.
		const draftTier = tier({ id: -1, pathId: 1, name: 'Brand New Tier', prerequisiteIds: [] });
		const gatedTier = tier({ prerequisiteIds: [] });
		const store = makeStore(gatedTier, { profs: [gatedTier, draftTier], tierTab: 'gateways' });
		render(TierDetail, { props: { store } });

		await fireEvent.change(screen.getByLabelText('Add prerequisite'), { target: { value: '-1' } });
		expect(store.addPrerequisite).toHaveBeenCalledWith(5, -1);
	});

	it('excludes a retired tier from the prerequisite options', () => {
		const retiredTier = tier({ id: 3, name: 'Retired Tier', retiredAt: '2026-01-01T00:00:00Z' });
		const gatedTier = tier({ prerequisiteIds: [] });
		const store = makeStore(gatedTier, { profs: [gatedTier, retiredTier], tierTab: 'gateways' });
		render(TierDetail, { props: { store } });

		const select = screen.getByLabelText('Add prerequisite') as HTMLSelectElement;
		expect(Array.from(select.options).some((o) => o.text.includes('Retired Tier'))).toBe(false);
	});
});
