interface ISkill {
	skillId: number;
	skillName: string;
	baseDamage: number;
	damageMultipliers: IAttributeMultiplier[];
	skillDesc: string;
	cooldownMS: number;
	iconPath: string;
}