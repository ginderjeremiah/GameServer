type Comparator<Type> = (currData: Type, objData: Type) => boolean;

type Action<Type> = (data: Type) => void;

type Converter<Type, Type2> = (data: Type) => Type2;

type CheckFunc<Type> = (data: Type) => boolean;

type ProgressionFunc = (percentComplete: number) => number

type SlotVariant = "equipped" | "inventory";

type ApiResponseTypes = {
    '/api/AdminTools/AddEditItemMods': void
    '/api/AdminTools/AddEditItems': void
    '/api/AdminTools/AddEditItemSlots': void
    '/api/AdminTools/AddEditTags': void
    '/api/AdminTools/SetTagsForItem': void
    '/api/AdminTools/SetTagsForItemMod': void
    '/api/Enemy/DefeatEnemy': DefeatResults
    '/api/Enemy/Enemies': EnemyData[]
    '/api/Enemy/NewEnemy': EnemyInstance | {cooldown: number}
    '/api/Item/Items': ItemData[]
    '/api/Item/SlotsForItem': ItemSlot[]
    '/api/Item/SlotTypes': SlotType[]
    '/api/ItemCategory/ItemCategories': ItemCategory[]
    '/api/ItemMod/ItemMods': ItemMod[]
    '/api/Player/AllData': PlayerData
    '/api/Player/Inventory': InventoryData
    '/api/Player/LogPreferences': LogPreferences
    '/api/Player/SaveLogPreferences': void
    '/api/Player/UpdateEquippedItems': void
    '/api/Player/UpdateInventorySlots': void
    '/api/Player/UpdatePlayerStats': BaseStats
    '/api/Skill/Skills': SkillData[]
    '/api/Tags/Tags': Tag[]
    '/api/Tags/TagsForItem': Tag[]
    '/api/Tags/TagsForItemMod': Tag[]
    '/api/Zone/Zones': ZoneData[]
    '/Login': {currentZone: number, playerData: PlayerData}
    '/LoginStatus': string
}

type ApiEndpoint = keyof ApiResponseTypes

type ApiResponseType = ApiResponseTypes[ApiEndpoint]

enum ChangeType {
    Edit = 0,
    Add = 1,
    Delete = 2
}