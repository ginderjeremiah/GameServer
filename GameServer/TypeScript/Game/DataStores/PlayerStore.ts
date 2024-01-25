/*class PlayerStore implements PlayerData {
    static #playerData: PlayerData
    static #statStore: PlayerStatStore

    constructor(playerData?: PlayerData) {
        if (playerData) {
            PlayerStore.#playerData = playerData;  
            PlayerStore.#statStore = new PlayerStatStore(playerData);    
        }
    }

    get name() {
        return PlayerStore.#playerData.name;
    }

    set name(newName: string) {
        PlayerStore.#playerData.name = newName;
    }

    get level() {
        return PlayerStore.#playerData.level;
    }

    set level(newLevel: number) {
        PlayerStore.#playerData.level = newLevel;
    }

    get exp() {
        return PlayerStore.#playerData.exp;
    }

    set exp(newExp: number) {
        PlayerStore.#playerData.exp = newExp;
    }

    get stats() {
        return PlayerStore.#playerData.stats;
    }

    get selectedSkills() {
        return PlayerStore.#playerData.selectedSkills;
    }

    get statPointsGained() {
        return PlayerStore.#playerData.statPointsGained;
    }

    get statPointsUsed() {
        return PlayerStore.#playerData.statPointsUsed;
    }

    addStatPoints(addend: number) {
        PlayerStore.#playerData.statPointsGained += addend;
    }

    useStatPoints(numUsed: number) {
        PlayerStore.#playerData.statPointsUsed += numUsed;
    }

    updateStats(statChanges: BaseStats) {
        DataManager.updatePlayerStats(statChanges).then(resp => {
            PlayerStore.#playerData.stats = resp
            PlayerStore.#playerData.statPointsUsed = keys(resp)
                .map(key => resp[key])
                .reduce((prev, current) => prev + current);
        });
        
    }
}*/