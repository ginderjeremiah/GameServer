import {
	ERarity,
	IAttributeMultiplier,
	ISkill,
	ISkillEffect,
	ESkillEffectTarget,
	ESkillAcquisition,
	EDamageType
} from '$lib/api';
import { Battler } from './battler';
import { calculateSkillDamage, scaledEffectAmount } from './battle-formulas';

export class Skill implements ISkill {
	id: number;
	name: string;
	baseDamage: number;
	damageMultipliers: IAttributeMultiplier[];
	effects: ISkillEffect[];
	description: string;
	cooldownMs: number;
	// The leaf damage type this skill's direct hits deal (#1320); the battle pipeline resolves it to the
	// attacker's amplification and defender's resistance attributes via the `applies` map.
	damageType: EDamageType;
	iconPath: string;
	// Carried to satisfy the ISkill contract this display-and-battle model implements; battle logic
	// never reads provenance (the acquisition flag is authoring intent, not a combat input).
	acquisition: ESkillAcquisition;
	// Likewise carried only to satisfy the contract: the battle never reads rarity (it is display
	// metadata + a server-side proficiency-XP tier weight, never a combat input), so it stays out of
	// the parity surface even though the field rides along on the model.
	rarityId: ERarity;
	// The skill's word of power — display metadata surfaced on the Synthesis screen, never a combat
	// input; carried only to satisfy the contract (like rarity/acquisition above).
	word: string;
	pronunciation: string;
	translation: string;
	chargeTime = 0;
	renderChargeTime = 0;
	owner: Battler;

	constructor(data: ISkill, owner: Battler) {
		this.id = data.id;
		this.name = data.name;
		this.baseDamage = data.baseDamage;
		this.damageMultipliers = data.damageMultipliers;
		this.effects = data.effects;
		this.description = data.description;
		this.cooldownMs = data.cooldownMs;
		this.damageType = data.damageType;
		this.iconPath = data.iconPath;
		this.acquisition = data.acquisition;
		this.rarityId = data.rarityId;
		this.word = data.word;
		this.pronunciation = data.pronunciation;
		this.translation = data.translation;
		this.owner = owner;
	}

	public calculateDamage() {
		return calculateSkillDamage(this, this.owner.attributes);
	}

	/** Applies this skill's effects when it fires: each {@link ESkillEffectTarget.Self} effect to the
	 *  casting owner, each {@link ESkillEffectTarget.Opponent} effect to the given opponent. Called after
	 *  the skill's damage is dealt, so a self damage-buff never boosts its own carrying hit. Each effect's
	 *  magnitude scales off the CASTER's ({@link owner}'s) attributes — regardless of which battler it
	 *  lands on — mirroring the backend `BattleContext.ApplySkillEffect`. The optional `onApplied`
	 *  callback — supplied only by the live engine, never the headless simulator — is invoked for every
	 *  application (each one stacks) with the battler it landed on and the resolved (scaled) amount, so the
	 *  combat log can announce it. */
	public applyEffects(opponent: Battler, onApplied?: (effect: ISkillEffect, target: Battler, amount: number) => void) {
		for (const effect of this.effects) {
			const target = effect.target === ESkillEffectTarget.Self ? this.owner : opponent;
			const amount = scaledEffectAmount(effect, this.owner.attributes);
			target.applyEffect(effect, amount);
			onApplied?.(effect, target, amount);
		}
	}
}
