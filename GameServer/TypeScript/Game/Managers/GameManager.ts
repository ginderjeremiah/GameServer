/// <reference path="ZoneManager.ts"/>
/// <reference path="LogManager.ts"/>
/// <reference path="BattleManager.ts"/>
/// <reference path="InventoryManager.ts"/>
/// <reference path="LoginManager.ts"/>
/// <reference path="DataManager.ts"/>

class GameManager {  
    static #inventoryManager : InventoryManager;
    static #battleManager: BattleManager;
    static #zoneManager : ZoneManager;
    static #cardGameManager : CardGameManager;
    static #playerData: PlayerData;

    static #resetDataFlag = false;
    static #lastTime : number;

    static async init() {
        //TODO: Make login occur before accessing game page
        await LoginManager.showLogin();
        const login = DataManager.login("", "").then((result) => {
            GameManager.#playerData = result.data.playerData;
            GameManager.#zoneManager = new ZoneManager(result.data.currentZone);
            GameManager.#battleManager = new BattleManager();
            AttributeManager.init();
        });
        ScreenManager.init();
        DataManager.initStatics();
        LogManager.init();
        //TODO: create loading screen on init to load data into data manager?
        GameManager.#inventoryManager = new InventoryManager();
        await login;
        GameManager.#startGame();
    }

    static #startGame() : void {
        GameManager.#cardGameManager = new CardGameManager();
        GameManager.#lastTime = 0;
        window.requestAnimationFrame(GameManager.#gameLoop);
    }

    static #gameLoop(ts : DOMHighResTimeStamp) : void {
        if (GameManager.#resetDataFlag) {
            GameManager.#resetDataFlag = false;
            //DataManager.resetSaveData();
            GameManager.#battleManager.reset();
            GameManager.#inventoryManager.reset();
            GameManager.#startGame();
            return;
        }
        let timeDelta = ts - GameManager.#lastTime;
        //console.log(timeDelta);
        GameManager.#lastTime = ts;
        GameManager.#battleManager.update(timeDelta);
        GameManager.#cardGameManager.update(timeDelta);
        TooltipManager.refresh();
        window.requestAnimationFrame(GameManager.#gameLoop);
    }

    static updateEquipmentStats () : void {
        //TODO pull equip bonuses from inventory and apply to player
    }
    
    static changeZone(amount : number) : void {
        this.#battleManager.reset(GameManager.#zoneManager.changeZone(amount));
    }

    static currentZone() {
        return this.#zoneManager.currentZoneId;
    }

    static getPlayerStats() : {statsVersion : number, baseStats : BaseStats, derivedStats : DerivedStats} {
        return this.#battleManager.getPlayerStats();
    }

    static getOpponent(battler: Battler) {
        return this.#battleManager.getOpponent(battler);
    }

    static resetSaveData() : void {
        GameManager.#resetDataFlag = true;
    }

    static getPlayerData() {
        return GameManager.#playerData;
    }

    static async addItems(items: InventoryItem[]) {
        GameManager.#inventoryManager.addItems(items, await DataManager.items);
    }

    static updateStats(statChanges: BaseStats) {
        DataManager.updatePlayerStats(statChanges).then(resp => {
            this.#playerData.stats = resp
            this.#battleManager.incrementPlayerStatsVersion();
            this.#playerData.statPointsUsed = keys(resp)
                .map(key => resp[key])
                .reduce((prev, current) => prev + current);
        });    
    }
} 