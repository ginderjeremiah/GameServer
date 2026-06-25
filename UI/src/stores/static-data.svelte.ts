import {
	IAttribute,
	IChallenge,
	IChallengeType,
	IEnemy,
	IItem,
	IItemMod,
	IPath,
	IProficiency,
	ISkill,
	IStatisticType,
	IZone
} from '$lib/api';

let zones = $state<IZone[]>();
let enemies = $state<IEnemy[]>();
let items = $state<IItem[]>();
let skills = $state<ISkill[]>();
let itemMods = $state<IItemMod[]>();
let attributes = $state<IAttribute[]>();
let challenges = $state<IChallenge[]>();
let challengeTypes = $state<IChallengeType[]>();
let statisticTypes = $state<IStatisticType[]>();
let proficiencies = $state<IProficiency[]>();
let paths = $state<IPath[]>();

/* The backing `$state` slots are genuinely `undefined` until the loading screen (or the silent
   session-resume path) populates them, so the getters honestly expose `T[] | undefined` rather than
   casting the pre-load `undefined` away. The `undefined` state is load-bearing — `loaded` and the
   reference-data orchestration (`reference-data.ts`) detect "not yet loaded" by the slot being
   null — so it must stay observable. Callers that may run before load already guard with `?.`/`?? []`. */
export const staticData = {
	get zones(): IZone[] | undefined {
		return zones;
	},
	set zones(value: IZone[] | undefined) {
		zones = value;
	},
	get enemies(): IEnemy[] | undefined {
		return enemies;
	},
	set enemies(value: IEnemy[] | undefined) {
		enemies = value;
	},
	get items(): IItem[] | undefined {
		return items;
	},
	set items(value: IItem[] | undefined) {
		items = value;
	},
	get skills(): ISkill[] | undefined {
		return skills;
	},
	set skills(value: ISkill[] | undefined) {
		skills = value;
	},
	get itemMods(): IItemMod[] | undefined {
		return itemMods;
	},
	set itemMods(value: IItemMod[] | undefined) {
		itemMods = value;
	},
	get attributes(): IAttribute[] | undefined {
		return attributes;
	},
	set attributes(value: IAttribute[] | undefined) {
		attributes = value;
	},
	get challenges(): IChallenge[] | undefined {
		return challenges;
	},
	set challenges(value: IChallenge[] | undefined) {
		challenges = value;
	},
	get challengeTypes(): IChallengeType[] | undefined {
		return challengeTypes;
	},
	set challengeTypes(value: IChallengeType[] | undefined) {
		challengeTypes = value;
	},
	get statisticTypes(): IStatisticType[] | undefined {
		return statisticTypes;
	},
	set statisticTypes(value: IStatisticType[] | undefined) {
		statisticTypes = value;
	},
	get proficiencies(): IProficiency[] | undefined {
		return proficiencies;
	},
	set proficiencies(value: IProficiency[] | undefined) {
		proficiencies = value;
	},
	get paths(): IPath[] | undefined {
		return paths;
	},
	set paths(value: IPath[] | undefined) {
		paths = value;
	},
	get loaded(): boolean {
		return [
			zones,
			enemies,
			items,
			skills,
			itemMods,
			attributes,
			challenges,
			challengeTypes,
			statisticTypes,
			proficiencies,
			paths
		].every((set) => set != null);
	}
};
