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
	IItemSlot,
	ILoginCredentials,
	ILoginData,
	ILogPreference,
	IPlayerData,
	ISetTagsData,
	ISkill,
	ISlotType,
	ITag,
	ITagCategory,
	IZone
} from "./Types"

export type ApiResponseTypes = {
	'/api/AdminTools/AddEditItemAttributes': undefined
	'/api/AdminTools/AddEditItemModAttributes': undefined
	'/api/AdminTools/AddEditItemMods': undefined
	'/api/AdminTools/AddEditItems': undefined
	'/api/AdminTools/AddEditItemSlots': undefined
	'/api/AdminTools/AddEditTags': undefined
	'/api/AdminTools/SetTagsForItem': undefined
	'/api/AdminTools/SetTagsForItemMod': undefined
	'/api/Attributes': IAttribute[]
	'/api/Enemies': IEnemy[]
	'/api/ItemCategories': IItemCategory[]
	'/api/ItemMods': IItemMod[]
	'/api/Items': IItem[]
	'/api/Items/SlotsForItem': IItemSlot[]
	'/api/Items/SlotTypes': ISlotType[]
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
	'/Login': ILoginData
	'/LoginStatus': undefined
}

export type ApiRequestTypes = {
	'/api/AdminTools/AddEditItemAttributes': IAddEditItemAttributesData
	'/api/AdminTools/AddEditItemModAttributes': IAddEditItemModAttributesData
	'/api/AdminTools/AddEditItemMods': IChange<IItemMod>[]
	'/api/AdminTools/AddEditItems': IChange<IItem>[]
	'/api/AdminTools/AddEditItemSlots': IChange<IItemSlot>[]
	'/api/AdminTools/AddEditTags': IChange<ITag>[]
	'/api/AdminTools/SetTagsForItem': ISetTagsData
	'/api/AdminTools/SetTagsForItemMod': ISetTagsData
	'/api/ItemMods': { refreshCache: boolean | undefined }
	'/api/Items': { refreshCache: boolean | undefined }
	'/api/Items/SlotsForItem': { itemId: number, refreshCache: boolean | undefined }
	'/api/Player/SaveLogPreferences': ILogPreference[]
	'/api/Player/UpdateInventorySlots': IInventoryUpdate[]
	'/api/Player/UpdatePlayerStats': IAttributeUpdate[]
	'/api/Tags/TagsForItem': { itemId: number }
	'/api/Tags/TagsForItemMod': { itemModId: number }
	'/Login': ILoginCredentials
}

export type ApiEndpoint = keyof ApiResponseTypes

export type ApiEndpointWithRequest = keyof ApiRequestTypes

export type ApiEndpointNoRequest = Exclude<ApiEndpoint, ApiEndpointWithRequest>

export type ApiResponseType = ApiResponseTypes[ApiEndpoint]