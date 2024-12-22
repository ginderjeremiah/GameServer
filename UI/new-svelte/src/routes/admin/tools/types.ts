import {
	AddEditItems,
	AddEditZones,
	AddEditEnemies,
	SetZoneEnemies,
	AddEditSkills,
	SetSkillMultipliers,
	SetEnemySkills,
	SetAttributeDistributions
} from './';

export const toolMap = {
	Enemies: {
		'Add/Edit Enemies': AddEditEnemies,
		SetAttributeDistributions,
		SetEnemySkills
	},
	Items: {
		'Add/Edit Items': AddEditItems
	},
	Skills: {
		'Add/Edit Skills': AddEditSkills,
		SetSkillMultipliers
	},
	Zones: {
		'Add/Edit Zones': AddEditZones,
		SetZoneEnemies
	}
};

export type ToolName = keyof typeof toolMap;
