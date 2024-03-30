/// <reference path="../Shared/Api/DataCache.ts"/>
/// <reference path="../Shared/Api/ApiRequest.ts"/>

class DataManager {
    // static #zones = new DataCache(this.#getZones);
    // static #enemies = new DataCache(this.#getEnemies);
    // static #items = new DataCache(this.#getItems);
    // static #skills = new DataCache(this.#getSkills);
    // static #itemMods = new DataCache(this.#getItemMods);
    static zones: ZoneData[];
    static enemies: EnemyData[];
    static items: ItemData[];
    static skills: SkillData[];
    static itemMods: ItemModData[];
    static attributes: AttributeData[];

    static async init() {
        const staticData = await Promise.all([
            this.#getZones(),
            this.#getEnemies(),
            this.#getItems(),
            this.#getSkills(),
            this.#getItemMods(),
            this.#getAttributes()
        ]);

        this.zones = staticData[0];
        this.enemies = staticData[1];
        this.items = staticData[2];
        this.skills = staticData[3];
        this.itemMods = staticData[4];
        this.attributes = staticData[5];
    }
    
    // static get zones() {
    //     return this.#zones.data;
    // }

    // static get enemies() {
    //     return this.#enemies.data;
    // }

    // static get items() {
    //     return this.#items.data;
    // }

    // static get skills() {
    //     return this.#skills.data;
    // }

    // static get itemMods() {
    //     return this .#itemMods.data;
    // }

    static async getPlayerData() {
        return ApiRequest.get('/api/Player/AllData');
    }
    
    static async getInventoryData() {
        return ApiRequest.get('/api/Player/Inventory');
    }

    static async updateInventorySlots(updates: InventoryUpdate[]) {
        return ApiRequest.post('/api/Player/UpdateInventorySlots', updates);
    }

    static async getLogPreferences() {
        return ApiRequest.get('/api/Player/LogPreferences');
    }

    static async saveLogPreferences(prefs: Dict<any>) {
        return ApiRequest.post('/api/Player/SaveLogPreferences', prefs)
    }

    static async newEnemy(zoneId?: number) {
        return Promise.all([
            ApiRequest.get('/api/Enemy/NewEnemy', {newZoneId: zoneId}), 
            DataManager.enemies
        ]).then(async (results): Promise<{enemyInstance: EnemyInstance, enemyData: EnemyData}> => {
            if (results[0].cooldown) {
                return delay(results[0].cooldown).then(async () => await this.newEnemy(zoneId))
            } else {
                return {enemyInstance: results[0].enemyInstance, enemyData: results[1][results[0].enemyInstance.enemyId]}
            }
        });
    }

    static async defeatEnemy(enemyInstance: EnemyInstance) {
        return new ApiRequest('/api/Enemy/DefeatEnemy').post(enemyInstance);
    }

    static async login(username: string, password: string) {
        const req = new ApiRequest("/Login");
        return req.post({username: username, password: password});
    }

    static async loginStatus() {
        return new ApiRequest('/LoginStatus').get();
    }

    static async updatePlayerStats(attUpds: AttributeUpdate[]) {
        return ApiRequest.post('/api/Player/UpdatePlayerStats', attUpds)
    }

    static async #getZones() {
        return ApiRequest.get('/api/Zone/Zones');
    }

    static async #getEnemies() {
        return ApiRequest.get('/api/Enemy/Enemies');
    }

    static async #getItems() {
        return ApiRequest.get('/api/Item/Items');
    }

    static async #getSkills() {
        return ApiRequest.get('/api/Skill/Skills');
    }

    static async #getItemMods() {
        return ApiRequest.get('/api/ItemMod/ItemMods');
    }

    static async #getAttributes() {
        return ApiRequest.get('/api/Attribute/Attributes');
    }

    #resetPlayerData(): void {
        /*DataManager.savePlayerData({
            "name": "Player",
            "level": 1,
            "exp": 0,
            "baseStats": {
                "Strength": 1,
                "Endurance": 1,
                "Intellect": 1,
                "Agility": 1,
                "Dexterity": 1,
                "Luck": 1
            },
            "skills": ["Punch", "Slap", "Fire Bolt"]
        });*/
    }

    #resetInventoryData(): void {
        //DataManager.saveInventoryData({"inventory": [], "equipped": []});
    }

    /*saveLogSettings(logSettings: LogSettings): void {
        window.localStorage.setItem("logSettings", JSON.stringify(logSettings));
    }


    resetSaveData(): void {
        this.#resetPlayerData();
        this.#resetInventoryData();
    }*/
}
