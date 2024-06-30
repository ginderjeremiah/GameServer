/// <reference path="ZoneManager.ts"/>
/// <reference path="LogManager.ts"/>
/// <reference path="BattleManager.ts"/>
/// <reference path="InventoryManager.ts"/>
/// <reference path="LoginManager.ts"/>
/// <reference path="DataManager.ts"/>
/// <reference path="ScreenManager.ts"/>

class GameManager {  
    static #inventoryManager : InventoryManager;
    static #battleManager: BattleManager;
    static #zoneManager : ZoneManager;
    static #cardGameManager : CardGameManager;
    static #lastTime : number;
    static playerData: IPlayerData;

    static async init() {
        ScreenManager.init();
        //TODO: Make login occur before accessing game page
        await LoginManager.showLogin();
        const login = await DataManager.login("", "");
        const newEnemy = DataManager.newEnemy();
        const dataInit = await Promise.all([
            DataManager.init(),
            DataManager.getLogPreferences()
        ])
        this.playerData = login.data.playerData;
        this.#zoneManager = new ZoneManager(login.data.currentZone);
        this.#inventoryManager = new InventoryManager(this.playerData.inventoryData);
        const equippedAtts = this.#inventoryManager.getEquippedStats();
        AttributeManager.init(this.playerData, equippedAtts);
        LogManager.init(dataInit[1]);
        this.#battleManager = new BattleManager(this.playerData, equippedAtts, await newEnemy);
        this.#cardGameManager = new CardGameManager();
        this.#lastTime = performance.now();
        window.requestAnimationFrame(this.#gameLoop);
    }

    //need to use full GameManager identifier because function is callback for window.requestAnimationFrame
    static #gameLoop(ts : DOMHighResTimeStamp) : void {
        const timeDelta = ts - GameManager.#lastTime;
        GameManager.#lastTime = ts;
        const battleResult = GameManager.#battleManager.update(timeDelta);
        if (battleResult) {
            GameManager.handleBattleResult(battleResult);
        }
        GameManager.#cardGameManager.update(timeDelta);
        TooltipManager.refresh();
        window.requestAnimationFrame(GameManager.#gameLoop);
    }

    static async handleBattleResult(battleResult: BattleResult) {
        if (battleResult.victory) {
            const defeatResponse = await DataManager.defeatEnemy(battleResult.enemyInstance);
            if (defeatResponse.status == 200 && defeatResponse.data.rewards) {
                const enemyData = DataManager.enemies;
                const rewards = defeatResponse.data.rewards
                this.grantExp(rewards.expReward);
                this.addItems(rewards.drops);
                LogManager.logMessage(enemyData[battleResult.enemyInstance.id].name + " was defeated!", "Enemy Defeated");
            } else {
                LogManager.logMessage("There was an error defeating the enemy.", "ERROR");
            }
            await delay(defeatResponse.data.cooldown);
        } else {
            LogManager.logMessage("You've been defeated!", "Enemy Defeated");
        }
        const newEnemy = await DataManager.newEnemy();
        this.#battleManager.reset(this.playerData, this.#inventoryManager.getEquippedStats(), newEnemy);
    }

    static grantExp(exp: number) {
        LogManager.logMessage("Earned " + formatNum(exp) + " exp.", "Exp")
        this.playerData.exp += exp;
        if (this.playerData.exp >= this.playerData.level * 100) {
            this.levelUp();
        }
    }

    static levelUp() {
        this.playerData.exp -= this.playerData.level * 100;
        this.playerData.level++;
        this.playerData.statPointsGained += 6;
        LogManager.logMessage("Congratulations, you leveled up!", "LevelUp");
        LogManager.logMessage("You are now level " + this.playerData.level + ".", "LevelUp");
    }

    static updateEquipmentStats() : void {
        const equipStats = this.#inventoryManager.updateEquipmentStats();
        AttributeManager.createAllAttributesUI(this.playerData, equipStats);
    }
    
    static async changeZone(amount : number) {
        if (this.#zoneManager.changeZone(amount)) {
            this.#battleManager.pause();
            const newEnemy = await DataManager.newEnemy(this.#zoneManager.currentZoneId);
            this.#battleManager.reset(this.playerData, this.#inventoryManager.getEquippedStats(), newEnemy);
        }
    }

    static getOpponent(battler: Battler) {
        return this.#battleManager.getOpponent(battler);
    }

    static async addItems(items: IInventoryItem[]) {
        this.#inventoryManager.addItems(items);
    }

    static async updateStats(statChanges: IAttributeUpdate[]) {
        const resp = await DataManager.updatePlayerStats(statChanges);
        this.playerData.attributes = resp;
        this.playerData.statPointsUsed += statChanges
            .map(chg => chg.amount)
            .reduce((prev, current) => prev + current);
        AttributeManager.createAllAttributesUI(this.playerData, this.#inventoryManager.getEquippedStats())
    }
} 