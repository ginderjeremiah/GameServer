/// <reference path="../Shared/Api/DataCache.ts"/>
/// <reference path="../Shared/Api/ApiRequest.ts"/>

class DataManager {
    static #zones = new DataCache(this.#getZones);
    static #enemies = new DataCache(this.#getEnemies);
    static #items = new DataCache(this.#getItems);
    static #skills = new DataCache(this.#getSkills);

    static get zones() {
        return this.#zones.data;
    }

    static get enemies() {
        return this.#enemies.data;
    }

    static get items() {
        return this.#items.data;
    }

    static get skills() {
        return this.#skills.data;
    }

    static async getPlayerData() {
        return ApiRequest.get('/api/Player/AllData');
    }
    
    static async getInventoryData() {
        return ApiRequest.get('/api/Player/Inventory');
    }

    static async updateInventorySlots(updates: InventoryUpdate[]) {
        return ApiRequest.post('/api/Player/UpdateInventorySlots', updates);
    }

    static async updateEquippedItems(equipped: InventoryUpdate[]) {
        return ApiRequest.post('/api/Player/UpdateEquippedItems', equipped);
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

    static async updatePlayerStats(stats: BaseStats) {
        return ApiRequest.post('/api/Player/UpdatePlayerStats', stats)
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

    static initStatics() {
        this.zones;
        this.enemies;
        this.items;
        this.skills;
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

    /*
    async fetchData() : Promise<ApiResponse[]> {
        //let fetches : Promise<Response>[] = [
        //    fetch("Enemies.json").then((response) => response.json()).then((data) => this.#enemies = data.Enemies),
        //    fetch("Zones.json").then((response) => response.json()).then((data) => this.#zones = data.Zones),
        //    fetch("Skills.json").then((response) => response.json()).then((data) => this.#skills = data.Skills),
        //    fetch("Items.json").then((response) => response.json()).then((data) => this.#items = data.Items)
        //]

        return Promise.all([
            {endp: 'api/Enemies/AllEnemies', then: (resp: ApiResponse) => this.#enemies = resp.json},
            {endp: 'api/Zones/AllZones', then: (resp: ApiResponse) => this.#zones = resp.json},
            {endp: 'api/Skills/AllSkills', then: (resp: ApiResponse) => this.#skills = resp.json},
            {endp: 'api/Items/AllItems', then: (resp: ApiResponse) => this.#items = resp.json}
        ].map(reqs => {
            const req = new ApiRequest(reqs.endp);
            return req.get().then(reqs.then);
        }));
        //return Promise.all(fetches);
    }
    */
}
