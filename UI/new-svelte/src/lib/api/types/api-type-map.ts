import type {
	IAddEditItemAttributesData,
	IAddEditItemModAttributesData,
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
	ISetTagsData,
	ISkill,
	ITag,
	ITagCategory,
	IZone
} from "./"

export type ApiResponseTypes = {
	'AdminTools/AddEditItemAttributes': undefined
	'AdminTools/AddEditItemModAttributes': undefined
	'AdminTools/AddEditItemMods': undefined
	'AdminTools/AddEditItemModSlots': undefined
	'AdminTools/AddEditItems': undefined
	'AdminTools/AddEditTags': undefined
	'AdminTools/SetTagsForItem': undefined
	'AdminTools/SetTagsForItemMod': undefined
	'Attributes': IAttribute[]
	'Enemies': IEnemy[]
	'ItemCategories': IItemCategory[]
	'ItemMods': IItemMod[]
	'ItemMods/ItemModTypes': IItemModType[]
	'Items': IItem[]
	'Items/SlotsForItem': IItemModSlot[]
	'Login': IPlayerData
	'Login/CreateAccount': undefined
	'Login/Status': undefined
	'Player': IPlayerData
	'Player/SaveLogPreferences': undefined
	'Player/UpdateInventorySlots': undefined
	'Player/UpdatePlayerStats': IBattlerAttribute[]
	'Skills': ISkill[]
	'Tags': ITag[]
	'Tags/TagCategories': ITagCategory[]
	'Tags/TagsForItem': ITag[]
	'Tags/TagsForItemMod': ITag[]
	'Zones': IZone[]
}

export type ApiRequestTypes = {
	'AdminTools/AddEditItemAttributes': IAddEditItemAttributesData
	'AdminTools/AddEditItemModAttributes': IAddEditItemModAttributesData
	'AdminTools/AddEditItemMods': IChange<IItemMod>[]
	'AdminTools/AddEditItemModSlots': IChange<IItemModSlot>[]
	'AdminTools/AddEditItems': IChange<IItem>[]
	'AdminTools/AddEditTags': IChange<ITag>[]
	'AdminTools/SetTagsForItem': ISetTagsData
	'AdminTools/SetTagsForItemMod': ISetTagsData
	'ItemMods': { refreshCache?: boolean }
	'Items': { refreshCache?: boolean }
	'Items/SlotsForItem': { itemId: number, refreshCache?: boolean }
	'Login': ILoginCredentials
	'Login/CreateAccount': ILoginCredentials
	'Player/SaveLogPreferences': ILogPreference[]
	'Player/UpdateInventorySlots': IInventoryUpdate[]
	'Player/UpdatePlayerStats': IAttributeUpdate[]
	'Tags/TagsForItem': { itemId: number }
	'Tags/TagsForItemMod': { itemModId: number }
}

export type ApiEndpoint = keyof ApiResponseTypes

export type ApiEndpointWithRequest = keyof ApiRequestTypes

export type ApiEndpointNoRequest = Exclude<ApiEndpoint, ApiEndpointWithRequest>

export type ApiResponseType = ApiResponseTypes[ApiEndpoint]