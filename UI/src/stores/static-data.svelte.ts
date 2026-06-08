import {
	IAttribute,
	IChallenge,
	IChallengeType,
	IEnemy,
	IItem,
	IItemMod,
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

export const staticData = {
	get zones() {
		return zones as IZone[];
	},
	set zones(value) {
		zones = value;
	},
	get enemies() {
		return enemies as IEnemy[];
	},
	set enemies(value) {
		enemies = value;
	},
	get items() {
		return items as IItem[];
	},
	set items(value) {
		items = value;
	},
	get skills() {
		return skills as ISkill[];
	},
	set skills(value) {
		skills = value;
	},
	get itemMods() {
		return itemMods as IItemMod[];
	},
	set itemMods(value) {
		itemMods = value;
	},
	get attributes() {
		return attributes as IAttribute[];
	},
	set attributes(value) {
		attributes = value;
	},
	get challenges() {
		return challenges as IChallenge[];
	},
	set challenges(value) {
		challenges = value;
	},
	get challengeTypes() {
		return challengeTypes as IChallengeType[];
	},
	set challengeTypes(value) {
		challengeTypes = value;
	},
	get statisticTypes() {
		return statisticTypes as IStatisticType[];
	},
	set statisticTypes(value) {
		statisticTypes = value;
	},
	get loaded() {
		return (
			zones && enemies && items && skills && itemMods && attributes && challenges && challengeTypes && statisticTypes
		);
	}
};
