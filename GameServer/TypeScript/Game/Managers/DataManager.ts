/// <reference path="../Shared/Api/ApiRequest.ts"/>

class DataManager {
    static zones: IZone[];
    static enemies: IEnemy[];
    static items: IItem[];
    static skills: ISkill[];
    static itemMods: IItemMod[];
    static attributes: IAttribute[];

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

    static async getPlayerData() {
        return ApiRequest.get('/api/Player');
    }
    
    static async getInventoryData() {
        return ApiRequest.get('/api/Player/Inventory');
    }

    static async updateInventorySlots(updates: IInventoryUpdate[]) {
        return ApiRequest.post('/api/Player/UpdateInventorySlots', updates);
    }

    static async getLogPreferences() {
        return ApiRequest.get('/api/Player/LogPreferences');
    }

    static async saveLogPreferences(prefs: ILogPreference[]) {
        return ApiRequest.post('/api/Player/SaveLogPreferences', prefs)
    }

    static async newEnemy(zoneId?: number) {
        return Promise.all([
            ApiRequest.get('/api/Enemies/NewEnemy', {newZoneId: zoneId}), 
            DataManager.enemies
        ]).then(async (results): Promise<{enemyInstance: IEnemyInstance, enemyData: IEnemy}> => {
            if (results[0].cooldown) {
                return delay(results[0].cooldown).then(async () => await this.newEnemy(zoneId))
            } else {
                return {enemyInstance: results[0].enemyInstance, enemyData: results[1][results[0].enemyInstance.enemyId]}
            }
        });
    }

    static async defeatEnemy(enemyInstance: IEnemyInstance) {
        return new ApiRequest('/api/Enemies/DefeatEnemy').post(enemyInstance);
    }

    static async login(username: string, password: string) {
        return ApiRequest.post("/Login", {username: username, password: password});
    }

    static async loginStatus() {
        return new ApiRequest('/LoginStatus').get();
    }

    static async updatePlayerStats(attUpds: IAttributeUpdate[]) {
        return ApiRequest.post('/api/Player/UpdatePlayerStats', attUpds)
    }

    static async #getZones() {
        return ApiRequest.get('/api/Zones');
    }

    static async #getEnemies() {
        return ApiRequest.get('/api/Enemies');
    }

    static async #getItems() {
        return ApiRequest.get('/api/Items');
    }

    static async #getSkills() {
        return ApiRequest.get('/api/Skills');
    }

    static async #getItemMods() {
        return ApiRequest.get('/api/ItemMods');
    }

    static async #getAttributes() {
        return ApiRequest.get('/api/Attributes');
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
