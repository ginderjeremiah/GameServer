import { IAttributeMultiplier } from "../"

export interface ISkill {
	id: number;
	name: string;
	baseDamage: number;
	damageMultipliers: IAttributeMultiplier[];
	description: string;
	cooldownMS: number;
	iconPath: string;
}