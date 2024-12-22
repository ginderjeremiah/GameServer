export enum EAttribute {
	Strength = 0,
	Endurance = 1,
	Intellect = 2,
	Agility = 3,
	Dexterity = 4,
	Luck = 5,
	MaxHealth = 6,
	Defense = 7,
	CooldownRecovery = 8,
	DropBonus = 9,
	CriticalChance = 10,
	CriticalDamage = 11,
	DodgeChance = 12,
	BlockChance = 13,
	BlockReduction = 14,
};

export enum EChangeType {
	Add = 0,
	Edit = 1,
	Delete = 2,
};

export enum EItemCategory {
	Helm = 1,
	Chest = 2,
	Leg = 3,
	Boot = 4,
	Weapon = 5,
	Accessory = 6,
};

export enum EItemModType {
	Component = 1,
	Prefix = 2,
	Suffix = 3,
};

export enum ELogSetting {
	Damage = 1,
	Debug = 2,
	Exp = 3,
	LevelUp = 4,
	Inventory = 5,
	EnemyDefeated = 6,
};