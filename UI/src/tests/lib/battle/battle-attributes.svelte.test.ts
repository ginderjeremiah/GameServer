import { describe, it, expect } from 'vitest';
import { flushSync } from 'svelte';
import { EAttribute } from '$lib/api';
import { statify } from '$lib/common';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import { EModifierType, EAttributeModifierSource, type AttributeModifier } from '$lib/battle/attribute-modifier';

// In the running app battlers are made reactive with `statify`, so their attribute values are read
// inside Svelte `$derived` (e.g. BattlerCard's MaxHealth bar). A `getValue` that recomputed lazily
// would perform a `$state` write mid-derivation — the illegal `state_unsafe_mutation` that broke the
// real-time fight in e2e while the (non-reactive) unit suites stayed green. These guard that reading
// the memoised values inside a derivation is a pure read.
describe('BattleAttributes under statify (reactive reads)', () => {
	it('reads getValue inside a $derived without an unsafe-mutation, and reacts to changes', () => {
		const attrs = statify(new BattleAttributes([{ attributeId: EAttribute.Endurance, amount: 10 }]));

		let derivedMaxHealth = 0;
		const cleanup = $effect.root(() => {
			const maxHealth = $derived(attrs.getValue(EAttribute.MaxHealth));
			$effect(() => {
				derivedMaxHealth = maxHealth;
			});
		});

		flushSync();
		// 50 + 20*Endurance(10) = 250.
		expect(derivedMaxHealth).toBe(250);

		const enduranceBuff: AttributeModifier = {
			attribute: EAttribute.Endurance,
			amount: 5,
			type: EModifierType.Additive,
			source: EAttributeModifierSource.PlayerStatPoints
		};
		attrs.addModifier(enduranceBuff);
		flushSync();
		// 50 + 20*15 = 350 — the derived recomputes off the new modifier set.
		expect(derivedMaxHealth).toBe(350);

		attrs.removeModifier(enduranceBuff);
		flushSync();
		expect(derivedMaxHealth).toBe(250);

		cleanup();
	});
});
