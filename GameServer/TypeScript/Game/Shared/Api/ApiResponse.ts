class ApiResponse<T extends ApiResponseType> {
    #r: XMLHttpRequest;
    #ev: ProgressEvent<EventTarget>;
    #responseJson?: { data: T, error: string}

    constructor(r: XMLHttpRequest, ev: ProgressEvent<EventTarget>) {
        this.#r = r;
        this.#ev = ev
    }

    public get status() {
        return this.#r.status;
    }

    public get data(): T {
        if (!this.responseJson.data && this.responseJson.error) {
            throw new Error(this.responseJson.error);
        }
        return this.responseJson.data;
    }

    public get error() {
        return this.responseJson.error
    }

    public get responseText() {
        return this.#r.responseText;
    }

    private get responseJson() {
        return this.#responseJson ??= JSON.parse(this.#r.responseText)
    }
}

type ApiResponseTypes = {
    '/api/AdminTools/AddEditItemMods': void
    '/api/AdminTools/AddEditItems': void
    '/api/AdminTools/AddEditItemSlots': void
    '/api/AdminTools/AddEditTags': void
    '/api/AdminTools/SetTagsForItem': void
    '/api/AdminTools/SetTagsForItemMod': void
    '/api/Attribute/Attributes': AttributeData[]
    '/api/Enemy/DefeatEnemy': DefeatResults
    '/api/Enemy/Enemies': EnemyData[]
    '/api/Enemy/NewEnemy': NewEnemy
    '/api/Item/Items': ItemData[]
    '/api/Item/SlotsForItem': ItemSlot[]
    '/api/Item/SlotTypes': SlotType[]
    '/api/ItemCategory/ItemCategories': ItemCategory[]
    '/api/ItemMod/ItemMods': ItemModData[]
    '/api/Player/AllData': PlayerData
    '/api/Player/Inventory': InventoryData
    '/api/Player/LogPreferences': LogPreferences
    '/api/Player/SaveLogPreferences': void
    '/api/Player/UpdateInventorySlots': void
    '/api/Player/UpdatePlayerStats': PlayerAttribute[]
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