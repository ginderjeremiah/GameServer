import type {
	IAddEditAttributesData,
	IAttribute,
	IAttributeUpdate,
	IBattlerAttribute,
	IChange,
	IEnemy,
	IInventoryUpdate,
	IItem,
	IItemCategory,
	IItemMod,
	IItemModSlot,
	IItemModType,
	ILoginCredentials,
	ILogPreference,
	IPlayerData,
	ISetEnemyAttributeDistributions,
	ISetEnemySkillsData,
	ISetTagsData,
	ISetZoneEnemiesData,
	ISkill,
	ITag,
	ITagCategory,
	IZone,
	IZoneEnemy
} from "./"

export type ApiResponseTypes = {
	'AdminTools/AddEditEnemies': undefined;
	'AdminTools/AddEditItemAttributes': undefined;
	'AdminTools/AddEditItemModAttributes': undefined;
	'AdminTools/AddEditItemMods': undefined;
	'AdminTools/AddEditItemModSlots': undefined;
	'AdminTools/AddEditItems': undefined;
	'AdminTools/AddEditSkills': undefined;
	'AdminTools/AddEditTags': undefined;
	'AdminTools/AddEditZones': undefined;
	'AdminTools/SetEnemyAttributeDistributions': undefined;
	'AdminTools/SetEnemySkills': undefined;
	'AdminTools/SetSkillMultipliers': undefined;
	'AdminTools/SetTagsForItem': undefined;
	'AdminTools/SetTagsForItemMod': undefined;
	'AdminTools/SetZoneEnemies': undefined;
	'Attributes': IAttribute[];
	'Enemies': IEnemy[];
	'ItemCategories': IItemCategory[];
	'ItemMods': IItemMod[];
	'ItemMods/ItemModTypes': IItemModType[];
	'Items': IItem[];
	'Items/SlotsForItem': IItemModSlot[];
	'Login': IPlayerData;
	'Login/CreateAccount': undefined;
	'Login/Status': IPlayerData;
	'Player': IPlayerData;
	'Player/SaveLogPreferences': undefined;
	'Player/UpdateInventorySlots': undefined;
	'Player/UpdatePlayerStats': IBattlerAttribute[];
	'Skills': ISkill[];
	'Tags': ITag[];
	'Tags/TagCategories': ITagCategory[];
	'Tags/TagsForItem': ITag[];
	'Tags/TagsForItemMod': ITag[];
	'Zones': IZone[];
	'Zones/ZoneEnemies': IZoneEnemy[];
};

export type ApiRequestTypes = {
	'AdminTools/AddEditEnemies': IChange<IEnemy>[];
	'AdminTools/AddEditItemAttributes': IAddEditAttributesData;
	'AdminTools/AddEditItemModAttributes': IAddEditAttributesData;
	'AdminTools/AddEditItemMods': IChange<IItemMod>[];
	'AdminTools/AddEditItemModSlots': IChange<IItemModSlot>[];
	'AdminTools/AddEditItems': IChange<IItem>[];
	'AdminTools/AddEditSkills': IChange<ISkill>[];
	'AdminTools/AddEditTags': IChange<ITag>[];
	'AdminTools/AddEditZones': IChange<IZone>[];
	'AdminTools/SetEnemyAttributeDistributions': ISetEnemyAttributeDistributions;
	'AdminTools/SetEnemySkills': ISetEnemySkillsData;
	'AdminTools/SetSkillMultipliers': IAddEditAttributesData;
	'AdminTools/SetTagsForItem': ISetTagsData;
	'AdminTools/SetTagsForItemMod': ISetTagsData;
	'AdminTools/SetZoneEnemies': ISetZoneEnemiesData;
	'Enemies': { refreshCache?: boolean } | undefined;
	'ItemMods': { refreshCache?: boolean } | undefined;
	'Items': { refreshCache?: boolean } | undefined;
	'Items/SlotsForItem': { itemId: number, refreshCache?: boolean };
	'Login': ILoginCredentials;
	'Login/CreateAccount': ILoginCredentials;
	'Player/SaveLogPreferences': ILogPreference[];
	'Player/UpdateInventorySlots': IInventoryUpdate[];
	'Player/UpdatePlayerStats': IAttributeUpdate[];
	'Skills': { refreshCache?: boolean } | undefined;
	'Tags/TagsForItem': { itemId: number };
	'Tags/TagsForItemMod': { itemModId: number };
	'Zones': { refreshCache?: boolean } | undefined;
	'Zones/ZoneEnemies': { zoneId: number };
};

export type ApiEndpoint = keyof ApiResponseTypes;

export type ApiEndpointOptionalRequest = {
	[K in keyof ApiRequestTypes]: undefined extends ApiRequestTypes[K] ? K : never;
}[keyof ApiRequestTypes];

export type ApiEndpointWithRequest = keyof ApiRequestTypes;

export type ApiEndpointNoRequest = ApiEndpointOptionalRequest | Exclude<ApiEndpoint, ApiEndpointWithRequest>;

export type ApiResponseType = ApiResponseTypes[ApiEndpoint];