import type { IAttributeMultiplier } from "../"

export interface ISkill {
	id: number;
	name: string;
	baseDamage: number;
	damageMultipliers: IAttributeMultiplier[];
	description: string;
	cooldownMs: number;
	iconPath: string;
};