import {
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
	IItemModSlotType,
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
	'/api/Attributes': IAttribute[]
	'/api/Enemies': IEnemy[]
	'/api/ItemCategories': IItemCategory[]
	'/api/ItemMods': IItemMod[]
	'/api/Items': IItem[]
	'/api/Items/ItemModSlotTypes': IItemModSlotType[]
	'/api/Items/SlotsForItem': IItemModSlot[]
	'/api/Player': IPlayerData
	'/api/Player/SaveLogPreferences': undefined
	'/api/Player/UpdateInventorySlots': undefined
	'/api/Player/UpdatePlayerStats': IBattlerAttribute[]
	'/api/Skills': ISkill[]
	'/api/Tags': ITag[]
	'/api/Tags/TagCategories': ITagCategory[]
	'/api/Tags/TagsForItem': ITag[]
	'/api/Tags/TagsForItemMod': ITag[]
	'/api/Zones': IZone[]
	'~/api/Login': IPlayerData
	'api/AdminTools/AddEditItemAttributes': undefined
	'api/AdminTools/AddEditItemModAttributes': undefined
	'api/AdminTools/AddEditItemMods': undefined
	'api/AdminTools/AddEditItemModSlots': undefined
	'api/AdminTools/AddEditItems': undefined
	'api/AdminTools/AddEditTags': undefined
	'api/AdminTools/SetTagsForItem': undefined
	'api/AdminTools/SetTagsForItemMod': undefined
	'api/Login/Status': undefined
}

export type ApiRequestTypes = {
	'/api/ItemMods': { refreshCache?: boolean }
	'/api/Items': { refreshCache?: boolean }
	'/api/Items/SlotsForItem': { itemId: number, refreshCache?: boolean }
	'/api/Player/SaveLogPreferences': ILogPreference[]
	'/api/Player/UpdateInventorySlots': IInventoryUpdate[]
	'/api/Player/UpdatePlayerStats': IAttributeUpdate[]
	'/api/Tags/TagsForItem': { itemId: number }
	'/api/Tags/TagsForItemMod': { itemModId: number }
	'~/api/Login': ILoginCredentials
	'api/AdminTools/AddEditItemAttributes': IAddEditItemAttributesData
	'api/AdminTools/AddEditItemModAttributes': IAddEditItemModAttributesData
	'api/AdminTools/AddEditItemMods': IChange<IItemMod>[]
	'api/AdminTools/AddEditItemModSlots': IChange<IItemModSlot>[]
	'api/AdminTools/AddEditItems': IChange<IItem>[]
	'api/AdminTools/AddEditTags': IChange<ITag>[]
	'api/AdminTools/SetTagsForItem': ISetTagsData
	'api/AdminTools/SetTagsForItemMod': ISetTagsData
}

export type ApiEndpoint = keyof ApiResponseTypes

export type ApiEndpointWithRequest = keyof ApiRequestTypes

export type ApiEndpointNoRequest = Exclude<ApiEndpoint, ApiEndpointWithRequest>

export type ApiResponseType = ApiResponseTypes[ApiEndpoint]