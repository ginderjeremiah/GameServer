type ApiResponseTypes = {
	'/api/AdminTools/AddEditItemMods': undefined
	'/api/AdminTools/AddEditItems': undefined
	'/api/AdminTools/AddEditItemSlots': undefined
	'/api/AdminTools/AddEditTags': undefined
	'/api/AdminTools/SetTagsForItem': undefined
	'/api/AdminTools/SetTagsForItemMod': undefined
	'/api/Attributes': IAttribute[]
	'/api/Enemies': IEnemy[]
	'/api/Enemies/DefeatEnemy': IDefeatEnemy
	'/api/Enemies/NewEnemy': INewEnemy
	'/api/ItemCategories': IItemCategory[]
	'/api/ItemMods': IItemMod[]
	'/api/Items': IItem[]
	'/api/Items/SlotsForItem': IItemSlot[]
	'/api/Items/SlotTypes': ISlotType[]
	'/api/Player': IPlayerData
	'/api/Player/Inventory': IInventoryData
	'/api/Player/LogPreferences': ILogPreference[]
	'/api/Player/SaveLogPreferences': undefined
	'/api/Player/UpdateInventorySlots': undefined
	'/api/Player/UpdatePlayerStats': IBattlerAttribute[]
	'/api/Skills': ISkill[]
	'/api/Tags': ITag[]
	'/api/Tags/TagsForItem': ITag[]
	'/api/Tags/TagsForItemMod': ITag[]
	'/api/Zones': IZone[]
	'/Login': ILoginData
	'/LoginStatus': undefined
}

type ApiRequestTypes = {
	'/api/AdminTools/AddEditItemMods': IChange<IItemMod>[]
	'/api/AdminTools/AddEditItems': IChange<IItem>[]
	'/api/AdminTools/AddEditItemSlots': IChange<IItemSlot>[]
	'/api/AdminTools/AddEditTags': IChange<ITag>[]
	'/api/AdminTools/SetTagsForItem': ISetTagsData
	'/api/AdminTools/SetTagsForItemMod': ISetTagsData
	'/api/Enemies/DefeatEnemy': IEnemyInstance
	'/api/Enemies/NewEnemy': { newZoneId: number | undefined }
	'/api/ItemMods': { refreshCache: boolean | undefined }
	'/api/Items/SlotsForItem': { itemId: number, refreshCache: boolean | undefined }
	'/api/Player/SaveLogPreferences': ILogPreference[]
	'/api/Player/UpdateInventorySlots': IInventoryUpdate[]
	'/api/Player/UpdatePlayerStats': IAttributeUpdate[]
	'/api/Tags/TagsForItem': { itemId: number }
	'/api/Tags/TagsForItemMod': { itemModId: number }
	'/Login': ILoginCredentials
}

type ApiEndpoint = keyof ApiResponseTypes

type ApiEndpointWithRequest = keyof ApiRequestTypes

type ApiEndpointNoRequest = Exclude<ApiEndpoint, ApiEndpointWithRequest>

type ApiResponseType = ApiResponseTypes[ApiEndpoint]