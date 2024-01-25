"use strict";
class ZoneManager {
    //#zones: ZoneData[];
    #orderedZones;
    #currentZone;
    #zoneNumDisplay;
    #zoneTitleDisplay;
    //#delayedAction: DelayedAction;
    constructor(currentZoneId) {
        this.#currentZone = 0;
        DataManager.zones.then(zones => this.#init(zones, currentZoneId));
        document.getElementById("zoneButtonLeft")?.addEventListener('click', (event) => {
            GameManager.changeZone(-1);
        });
        document.getElementById("zoneButtonRight")?.addEventListener('click', (event) => {
            GameManager.changeZone(1);
        });
        this.#zoneNumDisplay = document.getElementById("zoneNum");
        this.#zoneTitleDisplay = document.getElementById("zoneTitle");
        // this.#delayedAction = new DelayedAction(5000, () => (() => {
        //     DataManager.setCurrentZone(this.currentZoneId)
        // }).bind(this))
    }
    #init(zones, currentZoneId) {
        this.#orderedZones = zones.sort((one, two) => one.zoneOrder - two.zoneOrder);
        this.#currentZone = this.getOrderedIndex(currentZoneId);
        this.updateZoneDisplay();
    }
    changeZone(amount) {
        const newZone = this.#currentZone + amount;
        if (newZone >= 0 && newZone < this.#orderedZones.length) {
            this.#currentZone = newZone;
            this.updateZoneDisplay();
        }
        return this.currentZoneId;
    }
    updateZoneDisplay() {
        this.#zoneNumDisplay.textContent = "Zone " + this.#currentZone;
        this.#zoneTitleDisplay.textContent = this.#orderedZones[this.#currentZone].zoneName;
    }
    getZoneInfo() {
        return this.#orderedZones[this.#currentZone];
    }
    getOrderedIndex(zoneId) {
        for (let i = 0; i < this.#orderedZones.length; i++) {
            if (this.#orderedZones[i].zoneId === zoneId)
                return i;
        }
        return 0;
    }
    get currentZoneId() {
        return this.#orderedZones[this.#currentZone].zoneId;
    }
}
class LogManager {
    static #logBox = document.getElementById("logBox");
    static #logPreferences;
    static #updatedLogPreferences;
    static async init() {
        this.#logPreferences = await DataManager.getLogPreferences();
        this.#updatedLogPreferences = Object.assign({}, this.#logPreferences);
        document.getElementById("loggingOptionsButton").addEventListener('click', async (event) => {
            await PopupManager.triggerPopup("loggingOptionsPopup");
            LogManager.saveSettingChanges();
        });
        let loggingSettingsList = document.getElementById("loggingOptionsList");
        Object.keys(LogManager.#logPreferences).forEach((setting) => {
            let row = document.createElement("tr");
            loggingSettingsList.firstElementChild.appendChild(row);
            let label = document.createElement("td");
            row.appendChild(label);
            label.textContent = setting + ":";
            let dropdownCell = document.createElement("td");
            row.appendChild(dropdownCell);
            let dropdown = document.createElement("select");
            dropdown.className = "optionSelect";
            dropdown.addEventListener('change', (event) => {
                let target = event.target;
                LogManager.setSetting(setting, target.value == 'true');
            });
            dropdownCell.appendChild(dropdown);
            let enabled = document.createElement("option");
            enabled.label = "Enabled";
            enabled.value = "true";
            enabled.selected = LogManager.#logPreferences[setting];
            dropdown.appendChild(enabled);
            let disabled = document.createElement("option");
            disabled.label = "Disabled";
            disabled.value = "false";
            disabled.selected = !LogManager.#logPreferences[setting];
            dropdown.appendChild(disabled);
        });
    }
    static setSetting(id, value) {
        this.#updatedLogPreferences[id] = value;
    }
    static saveSettingChanges() {
        const changes = {};
        let changesExist = false;
        Object.keys(this.#logPreferences).forEach(pref => {
            if (this.#logPreferences[pref] !== this.#updatedLogPreferences[pref]) {
                changes[pref] = this.#updatedLogPreferences[pref];
                changesExist = true;
            }
        });
        if (changesExist) {
            Object.assign(this.#logPreferences, this.#updatedLogPreferences);
            DataManager.saveLogPreferences(changes);
        }
    }
    static logMessage(message, logType) {
        if (logType === "ERROR" || LogManager.#logPreferences[logType]) {
            let childElementCount = LogManager.#logBox.childElementCount;
            let newMessage = document.createElement('span');
            newMessage.className = "logMessage";
            newMessage.textContent = message;
            if (childElementCount >= 40) {
                LogManager.#logBox.lastChild.remove();
            }
            LogManager.#logBox.prepend(newMessage);
        }
    }
}
class BattleManager {
    #player;
    #enemy;
    #battleActive;
    #battleReady;
    static #battleManager;
    #tickSize = 6; //number of ms per logic tick
    #msStore = 0; //number of ms to be allocated to ticks
    constructor() {
        if (BattleManager.#battleManager) {
            throw new ReferenceError("BattleManager already exists.");
        }
        else {
            BattleManager.#battleManager = this;
            this.#battleActive = false;
            this.#battleReady = false;
            Promise.all([
                this.newEnemy(),
                DataManager.skills
            ]).then((results) => {
                this.#player = new Player(results[1]);
            });
        }
    }
    update(timeDelta) {
        this.#battleActive = this.#battleReady ? !(this.#battleReady = false) : this.#battleActive;
        if (this.#battleActive) {
            this.#msStore += timeDelta;
            while (this.#battleActive && this.#msStore > this.#tickSize) {
                this.#msStore -= this.#tickSize;
                this.computeTick();
                if (this.#enemy.isDead) {
                    this.#battleActive = false;
                    DataManager.defeatEnemy(this.#enemy.enemyInstance).then(response => {
                        const responseData = response.data;
                        if (response.status == 200 && responseData.rewards) {
                            LogManager.logMessage(this.#enemy.name + " was defeated!", "Enemy Defeated");
                            this.#player.grantExp(responseData.rewards.expReward);
                            GameManager.addItems(responseData.rewards.drops);
                        }
                        else {
                            LogManager.logMessage("There was an error defeating the enemy.", "ERROR");
                        }
                        delay(responseData.cooldown).then(() => this.reset());
                    });
                }
                else if (this.#player.isDead) {
                    LogManager.logMessage("You've been defeated!", "Enemy Defeated");
                    this.reset();
                }
            }
        }
    }
    computeTick() {
        const playerSkillsFired = this.#player.advanceCooldown(this.#tickSize);
        playerSkillsFired.forEach((skill) => {
            const dmg = skill.calculateDamage();
            let finalDmg = this.#enemy.takeDamage(dmg);
            LogManager.logMessage("You used " + skill.skillName + " and dealt " + formatNum(finalDmg) + " damage!", "Damage");
        });
        if (!this.#enemy.isDead) {
            const enemySkillsFired = this.#enemy.advanceCooldown(this.#tickSize);
            enemySkillsFired.forEach((skill) => {
                const dmg = skill.calculateDamage();
                let finalDmg = this.#player.takeDamage(dmg);
                LogManager.logMessage(this.#enemy.name + " used " + skill.skillName + " and dealt " + formatNum(finalDmg) + " damage!", "Damage");
            });
        }
    }
    reset(zoneId) {
        this.#battleActive = false;
        this.#battleReady = false;
        this.#enemy.clearSkillsDisplay();
        this.#player.reset();
        this.#msStore = 0;
        this.newEnemy(zoneId);
    }
    async newEnemy(zoneId) {
        return Promise.all([
            DataManager.newEnemy(zoneId),
            DataManager.skills
        ]).then((results) => {
            this.#enemy = new Enemy(results[0].enemyInstance, results[0].enemyData, results[1]);
            this.#battleReady = true;
        });
    }
    getPlayerStats() {
        return this.#player.stats;
    }
    getOpponent(battler) {
        return battler === this.#player ? this.#enemy : this.#player;
    }
    incrementPlayerStatsVersion() {
        this.#player.statsVersion++;
        this.#player.derivedStats = this.#player.calculateDerivativeStats();
    }
}
class InventoryManager {
    #maxInventoryItems = 23; //23 inventory slots and one trash slot
    inventory; //array containing inventory data
    inventorySlots = [];
    #inventoryDisplay = document.getElementById("inventoryContainer"); //reference to inventory HTML container
    equipped; //array Object containing equipped item data
    equippedIds = ["helmSlot", "chestSlot", "legSlot", "bootSlot", "weaponSlot", "accessorySlot"];
    equippedSlots = [];
    equippedDisplay = document.getElementById("equipSlotsContainer");
    #equippedStats; //JSON Object containing total stats of equipped items
    #equippedStatsList = document.getElementById("equipStatsList"); //reference to equipment HTML list for total equipped stats
    #delayedAction;
    constructor() {
        Promise.all([
            DataManager.getInventoryData(),
            DataManager.items
        ]).then((results) => this.#init(results[0], results[1]));
        this.#delayedAction = new DelayedAction(5000, (() => {
            DataManager.updateInventorySlots(this.getInventoryAsUpdates());
        }).bind(this));
    }
    #init(invData, items) {
        this.inventory = invData.inventory.map((i) => {
            return i ? new Item(i, items[i.itemId]) : i;
        });
        this.equipped = invData.equipped.map((i) => {
            return i ? new Item(i, items[i.itemId]) : i;
        });
        this.#equippedStats = {};
        this.#initializeInventorySlots();
        this.#initializeEquipmentSlots();
        this.updateEquipmentStats();
    }
    #initializeEquipmentSlots() {
        let del = this.deleteItem.bind(this);
        let swap = this.swap.bind(this);
        let eq = this.equipped;
        this.equippedIds.forEach((id, i) => {
            const index = i;
            let equipSlot = document.createElement('div');
            let dragged = false;
            equipSlot.id = id;
            equipSlot.className = "equipBox";
            equipSlot.addEventListener('drop', (event) => {
                event.preventDefault();
                let type = event.dataTransfer?.getData("text/slotType");
                if (event.dataTransfer?.getData("text/invItem") === "true" && type) {
                    swap(type, parseInt(event.dataTransfer.getData("text/plain")), "equipped", i);
                    equipSlot.classList.remove("highlight");
                    TooltipManager.setData(eq[index]);
                    TooltipManager.updatePosition(event.clientX, event.clientY);
                    TooltipManager.setLock();
                }
            });
            equipSlot.addEventListener('dragover', function (event) {
                event.preventDefault();
                if (!dragged) {
                    equipSlot.classList.add("highlight");
                }
            });
            equipSlot.addEventListener('dragleave', function (event) {
                equipSlot.classList.remove("highlight");
            });
            equipSlot.addEventListener('mouseenter', (event) => {
                if (eq[index]) {
                    TooltipManager.setData(eq[index]);
                    //TooltipManager.updatePosition(event.clientX, event.clientY);
                }
            });
            equipSlot.addEventListener('mousemove', (event) => {
                if (eq[index]) {
                    TooltipManager.updatePosition(event.clientX, event.clientY);
                }
            });
            equipSlot.addEventListener('mouseleave', (event) => {
                TooltipManager.remove();
            });
            equipSlot.addEventListener('dragstart', (event) => {
                if (eq[index]) {
                    TooltipManager.remove();
                    if (event.ctrlKey === true) {
                        del("equipped", i);
                        event.preventDefault();
                    }
                    else {
                        event.dataTransfer?.setData("text/plain", i.toString());
                        event.dataTransfer?.setData("text/slotType", "equipped");
                        event.dataTransfer?.setData("text/invItem", "true");
                        equipSlot.classList.add("darken");
                        dragged = true;
                    }
                }
                else {
                    event.preventDefault();
                }
            });
            equipSlot.addEventListener('dragend', (event) => {
                equipSlot.classList.remove("darken");
                dragged = false;
            });
            equipSlot.addEventListener('click', (event) => {
                if (event.ctrlKey === true) {
                    del("equipped", i);
                    TooltipManager.remove();
                }
            });
            equipSlot.draggable = true;
            this.equippedDisplay.appendChild(equipSlot);
            this.equippedSlots.push(equipSlot);
            const e = this.equipped[i];
            if (e) {
                this.#createItem(equipSlot, e.itemName);
            }
        });
    }
    #initializeInventorySlots() {
        let del = this.deleteItem.bind(this);
        let swap = this.swap.bind(this);
        let inv = this.inventory;
        for (let index = 0; index < this.#maxInventoryItems; index++) {
            const i = index;
            let invSlot = document.createElement('div');
            let dragged = false;
            invSlot.id = "iBox" + index;
            invSlot.className = "inventoryBox";
            invSlot.addEventListener('drop', (event) => {
                event.preventDefault();
                let type = event.dataTransfer?.getData("text/slotType");
                if (event.dataTransfer?.getData("text/invItem") === "true" && type) {
                    swap(type, parseInt(event.dataTransfer.getData("text/plain")), "inventory", index);
                    invSlot.classList.remove("highlight");
                    TooltipManager.setData(inv[i]);
                    TooltipManager.updatePosition(event.clientX, event.clientY);
                    TooltipManager.setLock();
                }
            });
            invSlot.addEventListener('dragover', function (event) {
                event.preventDefault();
                if (!dragged) {
                    invSlot.classList.add("highlight");
                }
            });
            invSlot.addEventListener('dragleave', function (event) {
                invSlot.classList.remove("highlight");
            });
            invSlot.addEventListener('mouseenter', (event) => {
                if (inv[i]) {
                    TooltipManager.setData(inv[i]);
                    //TooltipManager.updatePosition(event.clientX, event.clientY);
                }
            });
            invSlot.addEventListener('mousemove', (event) => {
                if (inv[i]) {
                    TooltipManager.updatePosition(event.clientX, event.clientY);
                }
            });
            invSlot.addEventListener('mouseleave', (event) => {
                TooltipManager.remove();
            });
            invSlot.addEventListener('dragstart', (event) => {
                if (inv[i]) {
                    TooltipManager.remove();
                    if (event.ctrlKey === true) {
                        del("inventory", index);
                        event.preventDefault();
                    }
                    else {
                        event.dataTransfer?.setData("text/plain", index.toString());
                        event.dataTransfer?.setData("text/slotType", "inventory");
                        event.dataTransfer?.setData("text/invItem", "true");
                        dragged = true;
                        invSlot.classList.add("darken");
                    }
                }
                else {
                    event.preventDefault();
                }
            });
            invSlot.addEventListener('dragend', (event) => {
                invSlot.classList.remove("darken");
                dragged = false;
            });
            invSlot.addEventListener('click', (event) => {
                if (event.ctrlKey === true) {
                    del("inventory", index);
                    TooltipManager.remove();
                }
            });
            invSlot.draggable = true;
            this.#inventoryDisplay.appendChild(invSlot);
            this.inventorySlots.push(invSlot);
            const item = this.inventory[index];
            if (item) {
                this.#createItem(invSlot, item.itemName);
            }
        }
        let trashSlot = document.createElement('div');
        trashSlot.id = "iBox" + this.#maxInventoryItems;
        trashSlot.className = "inventoryBox";
        trashSlot.addEventListener('drop', function (event) {
            event.preventDefault();
            let type = event.dataTransfer?.getData("text/slotType");
            if (event.dataTransfer?.getData("text/invItem") === "true" && type) {
                del(type, parseInt(event.dataTransfer.getData("text/plain")));
            }
        });
        trashSlot.addEventListener('dragover', function (event) {
            event.preventDefault();
        });
        /*trashSlot.addEventListener('mouseenter', (event) => {
            TooltipManager.setData(GameManager.getTrashData());
        });*/
        trashSlot.addEventListener('mousemove', (event) => {
            TooltipManager.updatePosition(event.clientX, event.clientY);
        });
        trashSlot.addEventListener('mouseleave', (event) => {
            TooltipManager.remove();
        });
        trashSlot.addEventListener('ondragstart', function (event) {
            return false;
        });
        this.#inventoryDisplay.appendChild(trashSlot);
        let trashIcon = document.createElement('img');
        trashIcon.className = "item";
        trashIcon.src = "img/Trash Can.png";
        trashIcon.draggable = false;
        trashSlot.appendChild(trashIcon);
    }
    //create an HTML element for item given its id and adds it to given slot
    #createItem(invSlot, item) {
        let invItem = document.createElement('img');
        invItem.draggable = false;
        invItem.className = "item";
        invItem.src = "img/" + item + ".png";
        invSlot.appendChild(invItem);
    }
    //move item in slot 1 to slot 2. Swap if necessary. Returns true if equip recalculation needs to occur.
    swap(slotType1, slot1, slotType2, slot2) {
        const base1 = this.getSlotContainers(slotType1);
        const base2 = this.getSlotContainers(slotType2);
        const box1 = base1.slots[slot1];
        const box2 = base2.slots[slot2];
        const source = base1.items[slot1];
        const target = base2.items[slot2];
        if (source) {
            base2.items[slot2] = source;
            if (target) {
                if (box2.firstChild) {
                    box1.appendChild(box2.firstChild);
                }
                base1.items[slot1] = target;
                target.slotId = slot1;
            }
            else {
                delete base1.items[slot1];
            }
            if (box1.firstChild) {
                box2.appendChild(box1.firstChild);
            }
            source.slotId = slot2;
            this.startSave();
            if (slotType1 !== slotType2) {
                this.updateEquipmentStats();
                DataManager.updateEquippedItems(this.getEquippedAsUpdates());
                GameManager.updateEquipmentStats();
            }
        }
    }
    getSlotContainers(slotType) {
        switch (slotType) {
            case "equipped":
                return { items: this.equipped, slots: this.equippedSlots };
            case "inventory":
                return { items: this.inventory, slots: this.inventorySlots };
        }
    }
    //add items to first available inventory slots
    addItems(items, itemData) {
        items.forEach(invItem => {
            const item = new Item(invItem, itemData[invItem.itemId]);
            this.inventory[invItem.slotId] = item;
            LogManager.logMessage("You found a " + item.itemName + "!", "Inventory");
            this.#createItem(this.inventorySlots[invItem.slotId], item.itemName);
        });
    }
    deleteItem(type, slot) {
        let base = this.getSlotContainers(type);
        base.items[slot] = null;
        base.slots[slot]?.firstChild?.remove();
        this.startSave();
    }
    getItemInfo(type, slot) {
        return this.getSlotContainers(type).items[slot];
    }
    updateEquipmentStats() {
        this.#equippedStats = {};
        /*for (let i = 0; i < this.equipped.length; i++) {
            const e = this.equipped[i];
            if (e) {
                Object.keys(this.e.Stats).forEach((stat) => {
                    if (this.#equippedStats[stat]) {
                        this.#equippedStats[stat] += e.Stats[stat];
                    } else {
                        this.#equippedStats[stat] = e.Stats[stat];
                    }
                });
            }
        };*/
        let listItems = Object.keys(this.#equippedStats).map((stat) => {
            let listItem = document.createElement("li");
            listItem.textContent = stat + " +" + this.#equippedStats[stat];
            return listItem;
        });
        this.#equippedStatsList.replaceChildren(...listItems);
    }
    async startSave() {
        this.#delayedAction.start();
    }
    reset() {
        this.#inventoryDisplay.replaceChildren();
        ["helmSlot", "chestSlot", "legSlot", "bootSlot", "weaponSlot", "accessorySlot"].forEach((slot) => {
            let invSlot = document.getElementById(slot);
            invSlot.replaceChildren();
        });
    }
    getEquippedAsUpdates() {
        return this.equipped.flatMap((item) => item ? { inventoryItemId: item.inventoryItemId, slotId: item.slotId } : []);
    }
    getInventoryAsUpdates() {
        return this.inventory.flatMap((item) => item ? { inventoryItemId: item.inventoryItemId, slotId: item.slotId } : []);
    }
}
class PopupManager {
    static #popupContainer = document.getElementById("popupContainer");
    static #popups = document.getElementsByClassName("popup");
    static currentResolve;
    static #init = (() => {
        document.getElementById("popupOverlay")?.addEventListener("click", (event) => {
            PopupManager.clearPopups(true);
        });
    })();
    static clearPopups(cancelled) {
        this.#popupContainer.style.display = "none";
        if (this.currentResolve) {
            this.currentResolve(cancelled);
        }
        this.currentResolve = null;
    }
    static triggerPopup(popup) {
        this.#popupContainer.style.display = "block";
        for (let i = 0; i < this.#popups.length; i++) {
            let element = this.#popups.item(i);
            if (element.id === popup) {
                element.style.display = "block";
            }
            else {
                element.style.display = "none";
            }
        }
        return new Promise((resolve) => {
            this.currentResolve = resolve;
        });
    }
}
/// <reference path="PopupManager.ts"/>
class LoginManager {
    static #username = document.getElementById("Username");
    static #password = document.getElementById("Password");
    static #errorSpan = document.getElementById("loginFailedReason");
    static async showLogin() {
        const response = await DataManager.loginStatus();
        if (response.status !== 200) {
            return new Promise((res) => {
                this.#show(res);
            });
        }
        return;
    }
    static async attemptLogin() {
        const username = this.#username.value;
        const password = this.#password.value;
        if (!username) {
            this.#errorSpan.textContent = "ERROR: Invalid Username.";
        }
        else if (!password) {
            this.#errorSpan.textContent = "ERROR: Invalid Password.";
        }
        else {
            const req = DataManager.login(username, password);
            req.then((response) => {
                if (response.status === 200) {
                    PopupManager.clearPopups(false);
                }
                else {
                    this.#errorSpan.textContent = "ERROR: " + response.responseText;
                }
            });
        }
    }
    static async #show(res) {
        return PopupManager.triggerPopup("loginPopup").then(() => res(), () => this.#show(res));
    }
}
class DataCache {
    #data;
    #retrievalTask;
    #retriever;
    constructor(retriever) {
        this.#retriever = retriever;
        this.#retrievalTask = null;
    }
    get data() {
        if (this.#data) {
            return Promise.resolve(this.#data);
        }
        else if (!this.#retrievalTask) {
            this.#retrievalTask = this.#retriever().then(res => {
                this.#data = res;
                this.#retrievalTask = null;
                return res;
            });
        }
        return this.#retrievalTask;
    }
}
class ApiRequest {
    r;
    endpoint;
    constructor(endpoint) {
        this.r = new XMLHttpRequest();
        this.endpoint = endpoint;
    }
    get(urlParams) {
        const params = this.encodeParams(urlParams);
        const endpoint = params
            ? this.endpoint + '?' + params
            : this.endpoint;
        this.r.open('GET', endpoint, true);
        const r = this.r;
        return new Promise((resolved, rejected) => {
            r.onload = (ev) => resolved(new ApiResponse(r, ev));
            r.onerror = (ev) => rejected(new ApiResponse(r, ev));
            r.onabort = r.onerror;
            r.send();
        });
    }
    static get(endpoint, urlParams) {
        const request = new ApiRequest(endpoint);
        return new Promise(async (resolved) => resolved((await request.get(urlParams)).data));
    }
    post(payload) {
        this.r.open('POST', this.endpoint, true);
        const r = this.r;
        r.setRequestHeader('content-type', 'application/json');
        const p = payload;
        return new Promise((resolved, rejected) => {
            r.onload = (ev) => resolved(new ApiResponse(r, ev));
            r.onerror = (ev) => rejected(new ApiResponse(r, ev));
            r.onabort = r.onerror;
            r.send(JSON.stringify(p));
        });
    }
    static post(endpoint, payload) {
        const request = new ApiRequest(endpoint);
        return new Promise(async (resolved) => resolved((await request.post(payload)).data));
    }
    encodeParams(urlParams) {
        return keys(urlParams)
            .filter(key => urlParams[key] !== undefined)
            .map(key => key + '=' + window.encodeURIComponent(urlParams[key]))
            .join('&');
    }
}
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
    static async updateInventorySlots(updates) {
        return ApiRequest.post('/api/Player/UpdateInventorySlots', updates);
    }
    static async updateEquippedItems(equipped) {
        return ApiRequest.post('/api/Player/UpdateEquippedItems', equipped);
    }
    static async getLogPreferences() {
        return ApiRequest.get('/api/Player/LogPreferences');
    }
    static async saveLogPreferences(prefs) {
        return ApiRequest.post('/api/Player/SaveLogPreferences', prefs);
    }
    static async newEnemy(zoneId) {
        return Promise.all([
            ApiRequest.get('/api/Enemy/NewEnemy', { newZoneId: zoneId }),
            DataManager.enemies
        ]).then(async (results) => {
            if (!IsEnemyInstance(results[0])) {
                return delay(results[0].cooldown).then(async () => await this.newEnemy(zoneId));
            }
            else {
                return { enemyInstance: results[0], enemyData: results[1][results[0].enemyId] };
            }
        });
    }
    static async defeatEnemy(enemyInstance) {
        return new ApiRequest('/api/Enemy/DefeatEnemy').post(enemyInstance);
    }
    static async login(username, password) {
        const req = new ApiRequest("/Login");
        return req.post({ username: username, password: password });
    }
    static async loginStatus() {
        return new ApiRequest('/LoginStatus').get();
    }
    static async updatePlayerStats(stats) {
        return ApiRequest.post('/api/Player/UpdatePlayerStats', stats);
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
    #resetPlayerData() {
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
    #resetInventoryData() {
        //DataManager.saveInventoryData({"inventory": [], "equipped": []});
    }
}
/// <reference path="ZoneManager.ts"/>
/// <reference path="LogManager.ts"/>
/// <reference path="BattleManager.ts"/>
/// <reference path="InventoryManager.ts"/>
/// <reference path="LoginManager.ts"/>
/// <reference path="DataManager.ts"/>
class GameManager {
    static #inventoryManager;
    static #battleManager;
    static #zoneManager;
    static #cardGameManager;
    static #playerData;
    static #resetDataFlag = false;
    static #lastTime;
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
    static #startGame() {
        GameManager.#cardGameManager = new CardGameManager();
        GameManager.#lastTime = 0;
        window.requestAnimationFrame(GameManager.#gameLoop);
    }
    static #gameLoop(ts) {
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
    static updateEquipmentStats() {
        //TODO pull equip bonuses from inventory and apply to player
    }
    static changeZone(amount) {
        this.#battleManager.reset(GameManager.#zoneManager.changeZone(amount));
    }
    static currentZone() {
        return this.#zoneManager.currentZoneId;
    }
    static getPlayerStats() {
        return this.#battleManager.getPlayerStats();
    }
    static getOpponent(battler) {
        return this.#battleManager.getOpponent(battler);
    }
    static resetSaveData() {
        GameManager.#resetDataFlag = true;
    }
    static getPlayerData() {
        return GameManager.#playerData;
    }
    static async addItems(items) {
        GameManager.#inventoryManager.addItems(items, await DataManager.items);
    }
    static updateStats(statChanges) {
        DataManager.updatePlayerStats(statChanges).then(resp => {
            this.#playerData.stats = resp;
            this.#battleManager.incrementPlayerStatsVersion();
            this.#playerData.statPointsUsed = keys(resp)
                .map(key => resp[key])
                .reduce((prev, current) => prev + current);
        });
    }
}
/// <reference path="Managers/GameManager.ts"/>
GameManager.init();
class Renderable {
    lNode;
    renderInfo;
    hover = false;
    clicked = false;
}
class Tooltippable {
    toolTipId;
    constructor() {
        this.toolTipId = TooltipManager.getId();
    }
}
class Battler {
    currentHealth;
    statsSource;
    derivedStats;
    skills;
    maxSkills = 4;
    statsVersion = 0;
    skillSlots = [];
    constructor(statsSource, skillDatas) {
        this.statsSource = statsSource;
        this.derivedStats = this.calculateDerivativeStats();
        this.currentHealth = this.derivedStats.maxHealth;
        this.skills = statsSource().selectedSkills.map((skillId) => new Skill(skillDatas[skillId], this));
    }
    updateHealthDisplay() {
        this.healthLabel.textContent = formatNum(this.currentHealth) + "/" + this.derivedStats.maxHealth;
        this.healthBar.value = this.currentHealth;
        this.healthBar.max = this.derivedStats.maxHealth;
    }
    updateLvlDisplay() {
        this.lvlLabel.textContent = "Lvl: " + this.level;
    }
    initSkillsDisplay() {
        let skills = this.skills;
        for (let i = 0; i < this.maxSkills; i++) {
            let skillBox = document.createElement("div");
            skillBox.className = "skillBox";
            skillBox.id = this.label + "Skill" + i;
            skillBox.addEventListener('mousemove', (event) => {
                if (skills[i]) {
                    TooltipManager.updatePosition(event.clientX, event.clientY);
                }
            });
            skillBox.addEventListener('mouseenter', (event) => {
                if (skills[i]) {
                    TooltipManager.setData(skills[i]);
                }
            });
            skillBox.addEventListener('mouseleave', (event) => {
                TooltipManager.remove();
            });
            this.skillsContainer.appendChild(skillBox);
            const skill = skills[i];
            if (skill) {
                const skillElement = document.createElement("img");
                skillElement.className = "skill";
                skillElement.src = skill.iconPath;
                skillBox.appendChild(skillElement);
            }
            this.skillSlots.push(skillBox);
        }
    }
    updateSkillsDisplay() {
        for (let i = 0; i < this.skills.length; i++) {
            const skill = this.skills[i];
            if (skill) {
                let slot = this.skillSlots[i];
                slot.style.cssText = "--perc: " + formatNum(100 * skill.chargeTime / skill.cooldownMS) + "%";
            }
        }
    }
    //returns skills which fired
    advanceCooldown(timeDelta) {
        let firedSkills = [];
        let cdMultiplier = (1 + this.derivedStats.cooldownRecovery / 100);
        this.skills.forEach((skill) => {
            if (skill) {
                skill.chargeTime += timeDelta * cdMultiplier;
                if (skill.chargeTime >= skill.cooldownMS) {
                    firedSkills.push(skill);
                    skill.chargeTime = 0;
                }
            }
        });
        this.updateSkillsDisplay();
        return firedSkills;
    }
    //returns actual damage dealt
    takeDamage(rawDamage) {
        let damage = rawDamage - this.derivedStats.defense;
        if (damage <= 0) {
            damage = 0;
        }
        this.currentHealth -= damage;
        this.updateHealthDisplay();
        return damage;
    }
    get isDead() {
        return this.currentHealth <= 0;
    }
    calculateDerivativeStats() {
        this.statsVersion++;
        return {
            maxHealth: 50 + 20 * this.baseStats.endurance + 5 * this.baseStats.strength,
            defense: 2 + this.baseStats.endurance + 0.5 * this.baseStats.agility,
            cooldownRecovery: 0.4 * this.baseStats.agility + 0.1 * this.baseStats.dexterity,
            dropMod: Math.log10(this.baseStats.luck)
            //critChance;
            //critMulti;
            //dodge;
            //blockChance;
            //blockMulti; 
        };
    }
    get stats() {
        return { statsVersion: this.statsVersion, baseStats: this.baseStats, derivedStats: this.derivedStats };
    }
    get baseStats() {
        return this.statsSource().stats;
    }
    clearSkillsDisplay() {
        this.skillsContainer.replaceChildren();
    }
    reset() {
        //this.skillsContainer.replaceChildren();
        //this.skillSlots = [];
        //this.initSkillsDisplay();
        this.skills.forEach((skill) => {
            if (skill) {
                skill.chargeTime = 0;
            }
        });
        this.updateSkillsDisplay();
        this.currentHealth = this.derivedStats.maxHealth;
        this.updateHealthDisplay();
    }
}
/// <reference path="Battler.ts"/>
class Enemy extends Battler {
    name;
    level;
    drops;
    //droppedItems: InventoryItem[];
    //victory: boolean;
    enemyInstance;
    skillsContainer = document.getElementById("enemySkillsContainer");
    healthBar = document.getElementById("enemyHealth");
    healthLabel = document.getElementById("enemyHealthLabel");
    lvlLabel = document.getElementById("enemyLvl");
    nameLabel = document.getElementById("enemyName");
    label = "enemy";
    constructor(enemyInstance, enemyData, skillDatas) {
        super(() => ({ stats: enemyInstance.stats, selectedSkills: enemyData.selectedSkills }), skillDatas);
        this.enemyInstance = enemyInstance;
        this.name = enemyData.enemyName;
        this.drops = enemyData.enemyDrops;
        this.level = enemyInstance.enemyLevel;
        //this.droppedItems = enemyInstance.droppedItems;
        //this.victory = enemyInstance.victory;
        this.nameLabel.textContent = this.name;
        this.initSkillsDisplay();
        this.updateSkillsDisplay();
        this.updateHealthDisplay();
        this.updateLvlDisplay();
    }
}
/// <reference path="../Abstract/Tooltippable.ts"/>
class Item extends Tooltippable {
    inventoryItemId;
    playerId;
    rating;
    itemId;
    itemName;
    itemDesc;
    itemCategoryId;
    equipped;
    slotId;
    itemMods;
    constructor(invItem, itemData) {
        super();
        this.inventoryItemId = invItem.inventoryItemId;
        this.playerId = invItem.playerId;
        this.rating = invItem.rating;
        this.itemId = invItem.itemId;
        this.itemName = itemData.itemName;
        this.itemDesc = itemData.itemDesc;
        this.itemCategoryId = itemData.itemCategoryId;
        this.equipped = invItem.equipped;
        this.slotId = invItem.slotId;
        this.itemMods = invItem.itemMods;
    }
    updateTooltipData(tooltipTitle, tooltipContent, prevId) {
        if (this.toolTipId !== prevId) {
            tooltipTitle.textContent = this.itemName; //set title to item name (key)
            tooltipContent.replaceChildren(); //clear current content
            let statsHeader = document.createElement("h3");
            statsHeader.className = "tooltipHeader";
            statsHeader.textContent = "Stats:";
            tooltipContent.appendChild(statsHeader);
            let statsList = document.createElement("ul");
            statsList.className = "tooltipList";
            /*Object.keys(this.item.Stats).forEach((stat) => {
                let listItem = document.createElement("li");
                listItem.textContent = stat + " +" + this.item.Stats[stat];
                statsList.appendChild(listItem);
            });*/
            tooltipContent.appendChild(statsList);
            let descHeader = document.createElement("h3");
            descHeader.className = "tooltipHeader";
            descHeader.textContent = "Description:";
            tooltipContent.appendChild(descHeader);
            let desc = document.createElement("p");
            desc.className = "tooltipText";
            desc.textContent = this.itemDesc;
            tooltipContent.appendChild(desc);
        }
        return this.toolTipId;
    }
}
/// <reference path="Battler.ts"/>
class Player extends Battler {
    name;
    level;
    currentExp;
    maxSkills = 4;
    statPointsGained;
    statPointsUsed;
    skillsContainer = document.getElementById("playerSkillsContainer");
    healthBar = document.getElementById("playerHealth");
    healthLabel = document.getElementById("playerHealthLabel");
    lvlLabel = document.getElementById("playerLvl");
    nameLabel = document.getElementById("playerName");
    label = "player";
    constructor(skillDatas) {
        const playerData = GameManager.getPlayerData();
        super(GameManager.getPlayerData, skillDatas);
        this.name = playerData.playerName;
        this.level = playerData.level;
        this.currentExp = playerData.exp;
        this.statPointsGained = playerData.statPointsGained;
        this.statPointsUsed = playerData.statPointsUsed;
        this.updateLvlDisplay();
        this.initSkillsDisplay();
        this.updateSkillsDisplay();
        this.updateHealthDisplay();
        this.updateLvlDisplay();
        this.nameLabel.textContent = this.name;
    }
    grantExp(expReward) {
        LogManager.logMessage("Earned " + formatNum(expReward) + " exp.", "Exp");
        this.currentExp += expReward;
        if (this.currentExp >= this.level * 100) {
            this.levelUp();
        }
    }
    levelUp() {
        this.currentExp -= this.level * 100;
        this.level++;
        this.statPointsGained += 6;
        this.derivedStats = this.calculateDerivativeStats();
        this.currentHealth = this.derivedStats.maxHealth;
        this.updateHealthDisplay();
        this.updateLvlDisplay();
        LogManager.logMessage("Congratulations, you leveled up!", "LevelUp");
        LogManager.logMessage("You are now level " + this.level + ".", "LevelUp");
    }
    updateExpDisplay() {
        //TODO create meter for displaying exp?
    }
}
/// <reference path="../Abstract/Tooltippable.ts"/>
class Skill extends Tooltippable {
    skillId;
    skillName;
    chargeTime;
    baseDamage;
    damageMultipliers;
    skillDesc;
    cooldownMS;
    iconPath;
    owner;
    ownerStatsVersion = -1;
    target;
    constructor(data, owner) {
        super();
        this.skillId = data.skillId;
        this.skillName = data.skillName;
        this.baseDamage = data.baseDamage;
        this.damageMultipliers = data.damageMultipliers;
        this.skillDesc = data.skillDesc;
        this.cooldownMS = data.cooldownMS;
        this.chargeTime = 0;
        this.iconPath = data.iconPath;
        this.owner = owner;
    }
    updateTooltipData(tooltipTitle, tooltipContent, prevId) {
        const target = GameManager.getOpponent(this.owner);
        let remainingCd = (this.cooldownMS - this.chargeTime) / ((1 + this.owner.derivedStats.cooldownRecovery / 100) * 1000);
        tooltipTitle.textContent = this.skillName + " (" + remainingCd.toFixed(2) + "s)";
        if (this.toolTipId !== prevId) {
            this.ownerStatsVersion = this.owner.statsVersion;
            this.target = target;
            tooltipContent.replaceChildren();
            let damageHeader = document.createElement("h3");
            damageHeader.className = "tooltipHeader";
            damageHeader.textContent = "Damage:";
            tooltipContent.appendChild(damageHeader);
            let damageList = document.createElement("ul");
            damageList.className = "tooltipList";
            damageList.id = this.skillName + "_damageList";
            this.createDamageListContent(damageList, target);
            tooltipContent.appendChild(damageList);
            let descHeader = document.createElement("h3");
            descHeader.className = "tooltipHeader";
            descHeader.textContent = "Description:";
            tooltipContent.appendChild(descHeader);
            let desc = document.createElement("p");
            desc.className = "tooltipText";
            desc.textContent = "Descriptions coming soon!";
            tooltipContent.appendChild(desc);
        }
        else if (this.ownerStatsVersion !== this.owner.statsVersion) {
            this.ownerStatsVersion = this.owner.statsVersion;
            this.target = target;
            let damageList = document.getElementById(this.skillName + "_damageList");
            damageList.replaceChildren();
            this.createDamageListContent(damageList, target);
        }
        else if (this.target !== target) {
            this.target = target;
            let totDam = this.calculateDamage();
            let adjDamage = document.getElementById(this.skillName + '_aDmg');
            let adjDmg = (totDam - target.derivedStats.defense);
            adjDmg = adjDmg > 0 ? adjDmg : 0;
            adjDamage.textContent = "Adjusted Total: " + formatNum(adjDmg);
            let adjustedDamagePerSecond = document.getElementById(this.skillName + '_aDPS');
            let adjustedDps = adjDmg / (this.cooldownMS / 1000 / (1 + this.owner.derivedStats.cooldownRecovery / 100));
            adjustedDamagePerSecond.textContent = "Adjusted DPS: " + formatNum(adjustedDps) + '/s';
        }
        return this.toolTipId;
    }
    calculateDamage() {
        let dmg = this.baseDamage;
        this.damageMultipliers.forEach((dmgType) => {
            dmg += this.owner.baseStats[dmgType.attributeName] * dmgType.multiplier;
        });
        return dmg;
    }
    createDamageListContent(damageList, target) {
        let baseDam = document.createElement("li");
        baseDam.textContent = "Base: " + this.baseDamage;
        damageList.appendChild(baseDam);
        let totDam = this.baseDamage;
        this.damageMultipliers.forEach((mult) => {
            let multiplier = document.createElement("li");
            let dam = this.owner.baseStats[mult.attributeName] * mult.multiplier;
            multiplier.textContent = capitalize(mult.attributeName) + ": " + formatNum(dam) + " (" + formatNum(mult.multiplier) + "x)";
            totDam += dam;
            damageList.appendChild(multiplier);
        });
        let totalDam = document.createElement("li");
        totalDam.textContent = "Total: " + formatNum(totDam);
        damageList.appendChild(totalDam);
        let adjDamage = document.createElement("li");
        adjDamage.id = this.skillName + '_aDmg';
        let adjDmg = (totDam - target.derivedStats.defense);
        adjDmg = adjDmg > 0 ? adjDmg : 0;
        adjDamage.textContent = "Adjusted Total: " + formatNum(adjDmg);
        damageList.appendChild(adjDamage);
        let cooldown = document.createElement("li");
        let cd = this.cooldownMS / 1000 / (1 + this.owner.derivedStats.cooldownRecovery / 100);
        cooldown.textContent = "Cooldown: " + formatNum(cd) + "s";
        damageList.appendChild(cooldown);
        let damagePerSecond = document.createElement("li");
        let dps = totDam / cd;
        damagePerSecond.textContent = "DPS: " + formatNum(dps) + "/s";
        damageList.appendChild(damagePerSecond);
        let adjDamagePerSecond = document.createElement('li');
        adjDamagePerSecond.id = this.skillName + '_aDPS';
        let adjDps = adjDmg / cd;
        adjDamagePerSecond.textContent = "Adjusted DPS: " + formatNum(adjDps) + '/s';
        damageList.appendChild(adjDamagePerSecond);
    }
}
class Anim {
    duration;
    transformData;
    //cancelCondition;
    remainingDuration;
    relative;
    dirty;
    constructor(duration, transformData, relative) {
        this.duration = duration;
        this.remainingDuration = duration;
        this.transformData = transformData;
        this.relative = relative;
        this.dirty = 'z' in transformData;
    }
    calcAnimChange(delta, obj) {
        /*if(this.duration) {
            if(this.duration === this.remainingDuration) {
                this.transformData.xDelta = (this.transformData.x - obj.renderInfo.x) / this.duration;
                this.transformData.yDelta = (this.transformData.y - obj.renderInfo.y) / this.duration;
                this.transformData.rotDelta = (this.transformData.rotation - obj.renderInfo.rotation) / this.duration;
                this.transformData.scaleDelta = (this.transformData.scale - obj.renderInfo.scale) / this.duration;
            }
            if(this.remainingDuration > delta) {
                obj.renderInfo.x += this.transformData.xDelta * delta;
                obj.renderInfo.y += this.transformData.yDelta * delta;
                obj.renderInfo.rotation += this.transformData.rotation * delta;
                obj.renderInfo.scale += this.transformData.scaleDelta * delta;
                this.remainingDuration -= delta;
                return 0;
            } else {
                obj.renderInfo.x += this.transformData.xDelta * this.remainingDuration;
                obj.renderInfo.y += this.transformData.yDelta * this.remainingDuration;
                obj.renderInfo.rotation += this.transformData.rotation * this.remainingDuration;
                obj.renderInfo.scale += this.transformData.scaleDelta * this.remainingDuration;
                return delta - this.remainingDuration;
            }
        } else {
            obj.renderInfo.x = this.transformData.x;
            obj.renderInfo.y = this.transformData.y;
            obj.renderInfo.z = this.transformData.z;
            obj.renderInfo.rotation = this.transformData.rotation;
            obj.renderInfo.scale += this.transformData.scaleDelta;
            return delta;
        }*/
        if (this.duration) {
            if (this.remainingDuration > delta) {
                let progress = this.relative ? delta / this.duration : delta / this.remainingDuration;
                for (let key in this.transformData) {
                    obj.renderInfo.data[key] += this.relative ? this.transformData[key] * progress : (this.transformData[key] - obj.renderInfo.data[key]) * progress;
                }
                this.remainingDuration -= delta;
                return 0;
            }
            else {
                let remainder = this.remainingDuration / this.duration;
                for (let key in this.transformData) {
                    obj.renderInfo.data[key] += this.relative ? this.transformData[key] * remainder : this.transformData[key] - obj.renderInfo.data[key];
                }
                return delta - this.remainingDuration;
            }
        }
        else {
            for (let key in this.transformData) {
                obj.renderInfo.data[key] += this.relative ? this.transformData[key] : this.transformData[key] - obj.renderInfo.data[key];
            }
            return delta;
        }
    }
}
class AnimationSequence {
    obj;
    animations;
    cancelCondition;
    lNode;
    constructor(obj, animations, cancelCondition) {
        this.obj = obj;
        this.animations = animations;
        this.cancelCondition = cancelCondition;
    }
    calcAnimChange(delta) {
        if (this.cancelCondition(this.obj)) {
            return delta;
        }
        let remainingDelta = delta;
        while (this.animations.length > 0 && (remainingDelta = this.animations[0].calcAnimChange(remainingDelta, this.obj))) {
            if (this.animations[0].dirty) {
                this.obj.lNode.sortSelf();
            }
            this.animations.shift();
        }
        return remainingDelta;
    }
}
class Card extends Renderable {
    //renderInfo! : RenderInfo;
    cardImage;
    titleText;
    descriptionText;
    cardBaseImg;
    highlightedCardBaseImg;
    hover;
    clicked;
    //lNode! : ListNode<Renderable>;
    animationSequences;
    constructor(imgPath, title, description /*, renderInfo : RenderInfo*/) {
        super();
        this.hover = false;
        this.clicked = false;
        this.cardBaseImg = new Image();
        this.cardBaseImg.src = 'img/card/CardBase.png';
        this.highlightedCardBaseImg = new Image();
        this.highlightedCardBaseImg.src = 'img/card/HighlightedCardBase.png';
        this.cardImage = new CardImage(this, imgPath);
        this.titleText = new CardText(title, 50);
        this.descriptionText = new CardText(description, 30);
        this.animationSequences = new LinkedList((x, y) => true);
        //this.renderInfo = renderInfo;
    }
    draw(context) {
        let width = this.renderInfo.data.width * this.renderInfo.data.scale;
        let height = this.renderInfo.data.height * this.renderInfo.data.scale;
        let x = this.renderInfo.data.x - width / 2;
        let y = this.renderInfo.data.y - height / 2;
        context.save();
        context.translate(this.renderInfo.data.x, this.renderInfo.data.y);
        context.rotate((this.renderInfo.data.rotation) * Math.PI / 180);
        context.translate(-this.renderInfo.data.x, -this.renderInfo.data.y);
        if (this.hover) {
            context.drawImage(this.highlightedCardBaseImg, x - 10 * this.renderInfo.data.scale, y - 10 * this.renderInfo.data.scale, width + 20 * this.renderInfo.data.scale, height + 20 * this.renderInfo.data.scale);
        }
        else {
            context.drawImage(this.cardBaseImg, x, y, width, height);
        }
        this.cardImage.draw(context, x + 20 * this.renderInfo.data.scale, y + 80 * this.renderInfo.data.scale);
        this.titleText.draw(context, this.renderInfo.data.x, y + 20 * this.renderInfo.data.scale, this.renderInfo.data.scale);
        this.descriptionText.draw(context, this.renderInfo.data.x, y + 280 * this.renderInfo.data.scale, this.renderInfo.data.scale);
        context.restore();
    }
    getHit(x, y) {
        //TODO factor in rotation
        return this.renderInfo.data.hitTop < y && this.renderInfo.data.hitLeft < x && this.renderInfo.data.hitRight > x && this.renderInfo.data.hitBottom > y;
    }
    updateRenderInfo(renderInfo) {
        this.renderInfo = renderInfo;
    }
    addAnimationSequence(animSequence) {
        this.animationSequences.appendData(animSequence);
    }
    update(delta) {
        this.animationSequences.forEach((animSeq) => {
            if (animSeq.calcAnimChange(delta)) {
                animSeq.lNode.removeSelf();
            }
        });
    }
    onHover() {
        this.hover = true;
        this.renderInfo.onHoverExtension?.(this);
        return true;
    }
    exitHover() {
        this.hover = false;
        this.renderInfo.exitHoverExtension?.(this);
        return false;
    }
}
class CardGameManager {
    #cardGameCanvas;
    #context;
    #hand;
    #objLayers;
    #currHover = null;
    static mousePos = { x: 0, y: 0 };
    constructor() {
        this.#objLayers = {
            background: new ObjLayer,
            card: new ObjLayer
        };
        //this.#cardGameCanvas = document.getElementById('cardGameCanvas');
        this.#cardGameCanvas = document.getElementsByTagName('canvas')[0];
        this.#cardGameCanvas.addEventListener('mousemove', (event) => {
            CardGameManager.updateMousePos(event.offsetX, event.offsetY);
        });
        this.#context = this.#cardGameCanvas.getContext('2d');
        this.#context.textBaseline = 'top';
        this.#context.textAlign = 'center';
        this.#hand = new CardHand(this.#objLayers.card);
        this.#hand.addCard('img/card/Placeholder.png', 'Strike', 'Deal x dmg.');
        this.#hand.addCard('img/card/Placeholder.png', 'Block', 'Block x dmg.');
        this.#hand.addCard('img/card/Placeholder.png', 'Strike', 'Deal x dmg.');
        this.#hand.addCard('img/card/Placeholder.png', 'Block', 'Block x dmg.');
        this.#hand.addCard('img/card/Placeholder.png', 'Strike', 'Deal x dmg.');
    }
    update(timeDelta) {
        if (this.#cardGameCanvas.offsetParent !== null) {
            this.#context.clearRect(0, 0, this.#cardGameCanvas.width, this.#cardGameCanvas.height);
            let scale = this.#cardGameCanvas.width / this.#cardGameCanvas.getBoundingClientRect().width, x = CardGameManager.mousePos.x * scale, y = CardGameManager.mousePos.y * scale, hitObj, clicked = false;
            hitObj = this.#objLayers.card.getHit(x, y) || this.#objLayers.background.getHit(x, y);
            if (hitObj !== this.#currHover) {
                this.#currHover && (this.#currHover.exitHover());
                this.#currHover = hitObj;
                this.#currHover && (this.#currHover.onHover()) && (this.#currHover.clicked = clicked);
            }
            this.#objLayers.background.update(timeDelta);
            this.#objLayers.card.update(timeDelta);
            this.#objLayers.background.draw(this.#context);
            this.#objLayers.card.draw(this.#context);
        }
        else {
            //console.log('card game not visible');
        }
    }
    static updateMousePos(x, y) {
        CardGameManager.mousePos.x = x;
        CardGameManager.mousePos.y = y;
    }
}
class CardHand {
    cards;
    cardLayer;
    renderInfo = [[],
        [{ x: 960, y: 1000, rotation: 0, z: 1 }],
        [{ x: 900, y: 1005, rotation: -3, z: 1 }, { x: 1020, y: 1005, rotation: 3, z: 2 }],
        [{ x: 840, y: 1007, rotation: -5, z: 1 }, { x: 960, y: 1000, rotation: 0, z: 2 }, { x: 1080, y: 1007, rotation: 5, z: 3 }],
        [{ x: 780, y: 1015, rotation: -8, z: 1 }, { x: 900, y: 1005, rotation: -3, z: 2 }, { x: 1020, y: 1005, rotation: 3, z: 3 }, { x: 1140, y: 1015, rotation: 8, z: 4 }],
        [{ x: 720, y: 1020, rotation: -10, z: 1 }, { x: 840, y: 1007, rotation: -5, z: 2 }, { x: 960, y: 1000, rotation: 0, z: 3 }, { x: 1080, y: 1007, rotation: 5, z: 4 }, { x: 1200, y: 1020, rotation: 10, z: 5 }],
        [], []];
    hitTop = 870;
    hitBottom = 1125;
    hitboxInfo = [[],
        [{ left: 885, right: 1035 }],
        [{ left: 885, right: 1035 }, { left: 885, right: 1035 }],
        [{ left: 885, right: 1035 }, { left: 885, right: 1035 }, { left: 885, right: 1035 }],
        [{ left: 885, right: 1035 }, { left: 885, right: 1035 }, { left: 885, right: 1035 }, { left: 885, right: 1035 }],
        [{ left: 625, right: 765 }, { left: 765, right: 895 }, { left: 895, right: 1025 }, { left: 1025, right: 1155 }, { left: 1155, right: 1295 }],
        [], []];
    baseWidth = 300;
    baseHeight = 500;
    scaleFactor = 0.5;
    constructor(layer) {
        this.cards = [];
        this.cardLayer = layer;
    }
    /*
    drawCards(context, x, y) {
        let hovers = this.getHovers(x, y);
        context.save();
        context.translate(this.centerPoint.x, this.centerPoint.y);
        this.cards.forEach((card, i) => {
            context.save();
            context.rotate((-20 + 10 * i) * Math.PI / 180);
            card.draw(context, -150, -700 - 500, hovers[i]);
            context.restore();
        })
        context.restore();
    }

    getHovers(x, y) {
        let hovers = Array(this.cards.length).fill(false);
        for (let i = this.cards.length - 1; i >= 0; i--) {
            let rotateAngle = (20 - 10 * i) * Math.PI / 180,
                cos = Math.cos(rotateAngle),
                sin = Math.sin(rotateAngle),
                localX = x - this.centerPoint.x,
                localY = y - this.centerPoint.y,
                transformX = cos * localX - sin * localY,
                transformY = sin * localX + cos * localY;
            if (transformX > -157 && transformX < 143 && transformY < -700 && transformY > -1205) {
                hovers[i] = true;
                return hovers;
            }
        }
        return hovers;
    }
    */
    addCard(imgPath, title, description) {
        let card = new Card(imgPath, title, description);
        this.cards.push(card);
        this.updateCardInfo();
        this.cardLayer.addObj(card);
    }
    updateCardInfo() {
        let renderInfo = this.renderInfo[this.cards.length];
        let hitboxInfo = this.hitboxInfo[this.cards.length];
        for (let i = 0; i < this.cards.length; i++) {
            this.cards[i].updateRenderInfo(new RenderInfo(renderInfo[i].x, renderInfo[i].y, renderInfo[i].z, renderInfo[i].rotation, this.baseWidth, this.baseHeight, this.scaleFactor, this.hitTop, this.hitBottom, hitboxInfo[i].right, hitboxInfo[i].left, 0, (obj) => {
                const scale = this.scaleFactor;
                obj.addAnimationSequence(new AnimationSequence(obj, [
                    new Anim(0, {
                        //x:,
                        y: 880,
                        z: 20,
                        rotation: 0,
                        scale: scale * 1.5
                    }, false),
                    new Anim(200, {
                        y: -20
                    }, true)
                ], (obj) => {
                    return !obj.hover;
                }));
            }, (obj) => {
                const y = renderInfo[i].y;
                const z = renderInfo[i].z;
                const rot = renderInfo[i].rotation;
                const scale = this.scaleFactor;
                obj.addAnimationSequence(new AnimationSequence(obj, [
                    new Anim(0, {
                        //x:,
                        y: y - 20,
                        z: z,
                        rotation: rot,
                        scale: scale
                    }, false),
                    new Anim(200, {
                        y: y
                    }, false)
                ], (obj) => {
                    return obj.hover;
                }));
            }));
        }
    }
}
class CardImage {
    card;
    imgMarginTop = 80;
    imgMarginHorizontal = 20;
    aspectRatio = 16 / 9;
    img;
    constructor(card, imgPath) {
        this.card = card;
        this.img = new Image();
        this.img.src = imgPath;
    }
    draw(context, x, y) {
        let width = (this.card.renderInfo.data.width - this.imgMarginHorizontal * 2) * this.card.renderInfo.data.scale;
        let height = width / this.aspectRatio;
        context.drawImage(this.img, x, y, width, height);
    }
}
class CardText {
    text;
    fontSize;
    constructor(text, fontSize) {
        this.text = text;
        this.fontSize = fontSize;
    }
    draw(context, x, y, scale) {
        //context.font = Math.round(this.fontSize * scale) + "px sans-serif";
        context.font = this.fontSize * scale + "px sans-serif";
        context.fillText(this.text, x, y);
    }
}
class LoopAnimation extends AnimationSequence {
    currentAnim = 0;
    calcAnimChange(delta) {
        if (this.cancelCondition(this.obj)) {
            return delta;
        }
        let remainingDelta = delta;
        while (remainingDelta = this.animations[this.currentAnim].calcAnimChange(remainingDelta, this.obj)) {
            if (this.animations[this.currentAnim].dirty) {
                this.obj.lNode.sortSelf();
            }
            this.currentAnim = this.currentAnim + 1 % this.animations.length;
        }
        return remainingDelta;
    }
}
class ObjLayer {
    objList;
    dirty;
    constructor() {
        this.objList = new LinkedList((currData, objData) => objData.renderInfo.data.z > currData.renderInfo.data.z);
        this.dirty = false;
    }
    addObj(obj) {
        this.objList.insertSorted(obj);
    }
    draw(context) {
        /*if(this.dirty) {
            this.dirty = false;
            this.objList.sort();
        }*/
        this.objList.forEachFromEnd((obj) => obj.draw(context));
    }
    getHit(x, y) {
        /*if(this.dirty) {
            this.dirty = false;
            this.objList.sort();
        }*/
        return this.objList.first((obj) => obj.getHit(x, y));
    }
    update(delta) {
        this.objList.forEach((obj) => {
            obj.update(delta);
        });
    }
}
class RenderInfo {
    data;
    onHoverExtension;
    exitHoverExtension;
    constructor(x, y, z, rotation, width, height, scale, hitTop, hitBottom, hitRight, hitLeft, hitRotation, onHoverExtension, exitHoverExtension) {
        this.data = {
            x: x,
            y: y,
            z: z,
            rotation: rotation,
            width: width,
            height: height,
            scale: scale,
            hitTop: hitTop,
            hitBottom: hitBottom,
            hitRight: hitRight,
            hitLeft: hitLeft,
            hitRotation: hitRotation
        };
        this.onHoverExtension = onHoverExtension;
        this.exitHoverExtension = exitHoverExtension;
    }
    ;
}
class Transform {
    parameter;
    magnitude;
    progressionFunc;
    relative;
    percentComplete;
    progression;
    constructor(parameter, magnitude, relative, progressionFunc) {
        this.parameter = parameter;
        this.magnitude = magnitude;
        this.relative = relative;
        this.progressionFunc = progressionFunc;
        this.percentComplete = 0;
        this.progression = 0;
    }
    reset() {
        this.percentComplete = 0;
    }
    transform(obj, percentComplete) {
        let progDif = this.progressionFunc(percentComplete);
        if (this.relative) {
            obj.renderInfo.data[this.parameter] += obj.renderInfo.data[this.parameter] + progDif * this.magnitude;
        }
        else {
            obj.renderInfo.data[this.parameter] += (this.magnitude - obj.renderInfo.data[this.parameter]) / (progDif / (1 - this.progression));
        }
        this.percentComplete = percentComplete;
        this.progression += progDif;
    }
}
class TransformFactory {
    static newLinearTransform(parameter, magnitude, relative) {
        let t = new Transform(parameter, magnitude, relative, (percentComplete) => {
            return percentComplete - t.percentComplete;
        });
        return t;
    }
}
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
class AttributeManager {
    static #attTableLabels = document.getElementById("attTableLabels");
    static #attTableButtons = document.getElementById("attTableButtons");
    static #attTableNumbers = document.getElementById("attTableNumbers");
    static #statPointsValue = document.getElementById("statPointsValue");
    static #changedStats;
    static #availablePoints;
    static init() {
        //TODO: make buttons actually do stuff 
        //figure out best way to sync player data... maybe create class where all instances are backed by a single data object?
        //display derived stats here?
        const playerData = GameManager.getPlayerData();
        this.#resetChangedStats();
        this.#availablePoints = playerData.statPointsGained - playerData.statPointsUsed;
        this.#statPointsValue.textContent = this.#availablePoints.toString();
        Object.keys(playerData.stats).forEach(key => {
            const spanLabelDiv = document.createElement('div');
            const spanLabel = document.createElement('span');
            spanLabel.textContent = key + ': ';
            spanLabelDiv.append(spanLabel);
            this.#attTableLabels.append(spanLabelDiv);
            const spanNumberDiv = document.createElement('div');
            const spanNumber = document.createElement('span');
            spanNumber.id = key + '_number';
            spanNumber.textContent = playerData.stats[key].toString();
            spanNumberDiv.append(spanNumber);
            this.#attTableNumbers.append(spanNumberDiv);
            const buttonsDiv = document.createElement('div');
            const minusButton = document.createElement('button');
            const plusButton = document.createElement('button');
            minusButton.textContent = '-';
            minusButton.addEventListener('click', e => {
                AttributeManager.removeStat(key);
            });
            plusButton.textContent = '+';
            plusButton.addEventListener('click', e => {
                AttributeManager.addStat(key);
            });
            buttonsDiv.append(minusButton);
            buttonsDiv.append(plusButton);
            this.#attTableButtons.append(buttonsDiv);
        });
    }
    static #resetChangedStats() {
        this.#changedStats = {
            strength: 0,
            endurance: 0,
            intellect: 0,
            agility: 0,
            dexterity: 0,
            luck: 0
        };
    }
    static addStat(stat) {
        if (this.#availablePoints > 0) {
            this.#changedStats[stat] += 1;
            this.#availablePoints -= 1;
            const numberSpan = document.getElementById(stat + '_number');
            if (numberSpan) {
                numberSpan.textContent = (GameManager.getPlayerData().stats[stat] + this.#changedStats[stat]).toString();
            }
            this.#statPointsValue.textContent = this.#availablePoints.toString();
        }
    }
    static removeStat(stat) {
        if (GameManager.getPlayerData().stats[stat] + this.#changedStats[stat] > 1) {
            this.#changedStats[stat] -= 1;
            this.#availablePoints += 1;
            const numberSpan = document.getElementById(stat + '_number');
            if (numberSpan) {
                numberSpan.textContent = (GameManager.getPlayerData().stats[stat] + this.#changedStats[stat]).toString();
            }
            this.#statPointsValue.textContent = this.#availablePoints.toString();
        }
    }
    static saveStats() {
        GameManager.updateStats(this.#changedStats);
        this.#resetChangedStats();
    }
}
class LootManager {
    #items;
    constructor(items) {
        this.#items = items;
    }
    rollLoot(zoneLoot, enemyLoot, dropMod) {
        /*let drops: Dict<ItemData> = {};
        zoneLoot.forEach((item) => {
            if (Math.random() < this.#items[item].BaseDropRate * dropMod) {
                drops[item] = this.#items[item];
            }
        })
        enemyLoot.forEach((item) => {
            if (Math.random() < this.#items[item].BaseDropRate * dropMod) {
                drops[item] = this.#items[item];
            }
        })
        return drops;*/
    }
}
class ScreenManager {
    static #screens = document.getElementsByClassName("screen"); //reference to all screen elements
    static init() {
        document.getElementById("Fight").style.display = "inline-block"; //display Fight screen on startup
    }
    static changeScreen(newScreen) {
        LogManager.logMessage("Changing screen to: " + newScreen, "Debug");
        for (let i = 0; i < ScreenManager.#screens.length; i++) {
            let element = ScreenManager.#screens.item(i);
            element.style.display = element.id === newScreen ? "inline-block" : "none";
        }
    }
}
class TooltipManager {
    static tooltipElement = document.getElementById("tooltip");
    static tooltipTitle = document.getElementById("tooltipTitle");
    static tooltipContent = document.getElementById("tooltipContent");
    static displayedDataId = -1;
    static currData;
    static currPlayerStats;
    static active = false;
    static lockEnd = 0;
    static lockAmount = 10;
    static lockActive = false;
    static #tooltipIds = 0;
    static getId() {
        return this.#tooltipIds++;
    }
    //set lock to prevent erroneous mouse events from interfering with tooltip after a drag
    static setLock() {
        this.lockActive = true;
        this.lockEnd = Date.now() + this.lockAmount;
    }
    static checkLock() {
        return this.lockActive ? (this.lockActive = Date.now() < this.lockEnd) : false;
    }
    static setData(data) {
        if (!this.checkLock()) {
            this.currData = data;
            this.active = true;
        }
    }
    static refresh() {
        if (this.active) {
            this.displayedDataId = this.currData.updateTooltipData(this.tooltipTitle, this.tooltipContent, this.displayedDataId);
        }
    }
    static updatePosition(mouseX, mouseY) {
        this.active = true;
        //subtract 17px for scrollbar buffer
        if (this.tooltipElement.offsetWidth + mouseX + 15 < window.innerWidth - 17) {
            this.tooltipElement.style.left = (mouseX + 15) + "px";
            this.tooltipElement.style.right = "";
        }
        else {
            this.tooltipElement.style.left = "";
            this.tooltipElement.style.right = (window.innerWidth - mouseX + 15) + "px";
        }
        if (this.tooltipElement.offsetHeight + mouseY + 15 < window.innerHeight) {
            this.tooltipElement.style.top = (mouseY + 15) + "px";
            this.tooltipElement.style.bottom = "";
        }
        else {
            this.tooltipElement.style.top = "";
            this.tooltipElement.style.bottom = (window.innerHeight - mouseY + 15) + "px";
        }
        this.tooltipElement.style.display = "block";
    }
    static remove() {
        if (!this.checkLock()) {
            this.active = false;
            this.tooltipElement.style.display = "none";
        }
    }
}
class Mulberry32 {
    #seed;
    constructor(seed) {
        this.#seed = seed;
    }
    next() {
        this.#seed += 0x6D2B79F5;
        let t = Math.imul(this.#seed ^ (this.#seed >>> 15), 1 | this.#seed);
        t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    }
}
class LinkedList {
    head;
    tail;
    length;
    comparator;
    constructor(comparator) {
        this.head = null;
        this.tail = null;
        this.length = 0;
        this.comparator = comparator;
    }
    appendData(data) {
        let node = new ListNode(this, data);
        if (this.tail) {
            node.prev = this.tail;
            this.tail.next = node;
        }
        else {
            this.head = node;
        }
        this.tail = node;
        this.length++;
    }
    prependData(data) {
        let node = new ListNode(this, data);
        if (this.head) {
            node.next = this.head;
            this.head.prev = node;
        }
        else {
            this.tail = node;
        }
        this.head = node;
        this.length++;
    }
    insertSorted(data) {
        let newNode = new ListNode(this, data);
        if (this.head) {
            let curr = this.head;
            while (curr && !this.comparator(curr.data, data)) {
                curr = curr.next;
            }
            if (curr === this.head) {
                this.head.prev = newNode;
                newNode.next = this.head;
                this.head = newNode;
            }
            else if (curr) {
                newNode.prev = curr.prev;
                newNode.prev.next = newNode;
                newNode.next = curr;
                curr.prev = newNode;
            }
            else {
                this.tail.next = newNode;
                newNode.prev = this.tail;
                this.tail = newNode;
            }
        }
        else {
            this.head = newNode;
            this.tail = newNode;
        }
        this.length++;
    }
    forEach(func) {
        let curr = this.head;
        let next;
        while (curr) {
            next = curr.next;
            func(curr.data);
            curr = next;
        }
    }
    forEachFromEnd(func) {
        let curr = this.tail;
        let prev;
        while (curr) {
            prev = curr.prev;
            func(curr.data);
            curr = prev;
        }
    }
    select(func) {
        let results = [];
        let curr = this.head;
        while (curr) {
            results.push(func(curr.data));
            curr = curr.next;
        }
        return results;
    }
    first(func) {
        let curr = this.head;
        while (curr && !func(curr.data)) {
            curr = curr.next;
        }
        return curr?.data;
    }
    firstFromEnd(func) {
        let curr = this.tail;
        while (curr && !func(curr.data)) {
            curr = curr.prev;
        }
        return curr?.data;
    }
    sort() {
        if (this.head && this.head.next) {
            let curr = this.head.next;
            let prev = this.head;
            let next = curr.next;
            while (curr) {
                if (this.comparator(prev.data, curr.data)) {
                    if (curr.next) {
                        curr.next.prev = curr.prev;
                    }
                    curr.prev.next = curr.next;
                    prev = prev.prev;
                    while (prev && this.comparator(prev.data, curr.data)) {
                        prev = prev.prev;
                    }
                    curr.prev = prev;
                    if (prev) {
                        curr.next = prev.next;
                        curr.next.prev = curr;
                        curr.prev.next = curr;
                    }
                    else {
                        this.head.prev = curr;
                        curr.next = this.head;
                        this.head = curr;
                    }
                }
                curr = next;
                if (curr) {
                    prev = curr.prev;
                    next = curr.next;
                }
            }
        }
    }
    remove(data) {
        let curr = this.head;
        while (curr && curr.data !== data) {
            curr = curr.next;
        }
        if (curr === this.head) {
            this.head = curr.next;
        }
        if (curr === this.tail) {
            this.tail = curr.prev;
        }
        curr?.prev && (curr.prev.next = curr.next);
        curr?.next && (curr.next.prev = curr.prev);
    }
}
class ListNode {
    data;
    prev;
    next;
    list;
    constructor(list, data) {
        this.list = list;
        data.lNode = this;
        this.data = data;
        this.next = null;
        this.prev = null;
    }
    sortSelf() {
        if (this.prev && this.list.comparator(this.prev.data, this.data)) {
            if (this.list.tail === this) {
                this.list.tail = this.prev;
            }
            if (this.next) {
                this.next.prev = this.prev;
            }
            this.prev.next = this.next;
            let prev = this.prev.prev;
            while (prev && this.list.comparator(prev.data, this.data)) {
                prev = prev.prev;
            }
            this.prev = prev;
            if (prev) {
                this.next = prev.next;
                this.next.prev = this;
                this.prev.next = this;
            }
            else {
                this.list.head.prev = this;
                this.next = this.list.head;
                this.list.head = this;
            }
        }
        else if (this.next && !this.list.comparator(this.next.data, this.data)) {
            if (this.list.head === this) {
                this.list.head = this.next;
            }
            if (this.prev) {
                this.prev.next = this.next;
            }
            this.next.prev = this.prev;
            let next = this.next.next;
            while (next && !this.list.comparator(next.data, this.data)) {
                next = next.next;
            }
            this.next = next;
            if (next) {
                this.prev = next.prev;
                this.prev.next = this;
                this.next.prev = this;
            }
            else {
                this.list.tail.next = this;
                this.prev = this.list.tail;
                this.list.tail = this;
            }
        }
    }
    /*
    sortSelf() {
        if(this.prev && this.list.comparator(this.prev.data, this.data)) {
            if(this.list.tail === this) {
                this.list.tail = this.prev;
            }
            if(this.next) {
                this.next.prev = this.prev;
            }
            this.prev.next = this.next;
            let curr = this.prev;
            let prev = curr.prev;
            while(curr && )
        } else if (this.next && !this.list.comparator(this.prev.data, this.data))
    }
    */
    removeSelf() {
        if (this === this.list.head) {
            this.list.head = this.next;
        }
        if (this === this.list.tail) {
            this.list.tail = this.prev;
        }
        this.prev && (this.prev.next = this.next);
        this.next && (this.next.prev = this.prev);
    }
}
var ChangeType;
(function (ChangeType) {
    ChangeType[ChangeType["Edit"] = 0] = "Edit";
    ChangeType[ChangeType["Add"] = 1] = "Add";
    ChangeType[ChangeType["Delete"] = 2] = "Delete";
})(ChangeType || (ChangeType = {}));
// randomInt between num1 (inclusive) and num2 (exclusive)
function randomInt(num1, num2) {
    return Math.floor(num1 + Math.random() * (num2 - num1));
}
// formats a number to a specific string representation
function formatNum(num) {
    return "" + parseFloat(num.toFixed(2));
}
async function delay(delay) {
    return new Promise(res => {
        setTimeout(() => res(), delay);
    });
}
function keys(obj) {
    return obj ? Object.keys(obj) : [];
}
function capitalize(str) {
    return str[0].toUpperCase() + str.slice(1);
}
function normalizeText(str) {
    return capitalize(str).replaceAll(/([a-z])([A-Z])/g, "$1 $2");
}
function plural(str) {
    const last = str.length - 1;
    switch (str.at(last)) {
        case 'y':
            return str.slice(0, last) + 'ies';
        case 'x':
            return str + 'es';
        case 's':
            return str;
        default:
            return str + 's';
    }
}
function groupBy(arr, groupFn) {
    const ret = {};
    arr.forEach(t => {
        const key = groupFn(t);
        if (ret[key]) {
            ret[key].push(t);
        }
        else {
            ret[key] = [t];
        }
    });
    return ret;
}
function IsEnemyInstance(instance) {
    return instance.seed !== undefined;
}
class ApiResponse {
    #r;
    #ev;
    #responseJson;
    constructor(r, ev) {
        this.#r = r;
        this.#ev = ev;
    }
    get status() {
        return this.#r.status;
    }
    get data() {
        if (!this.responseJson.data && this.responseJson.error) {
            throw new Error(this.responseJson.error);
        }
        return this.responseJson.data;
    }
    get error() {
        return this.responseJson.error;
    }
    get responseText() {
        return this.#r.responseText;
    }
    get responseJson() {
        return this.#responseJson ??= JSON.parse(this.#r.responseText);
    }
}
class DelayedAction {
    #action;
    #delayLength;
    #triggerStartTime;
    constructor(delayInMs, action) {
        this.#delayLength = delayInMs;
        this.#action = action;
        this.#triggerStartTime = 0;
    }
    async start() {
        this.#triggerStartTime = Date.now();
        await delay(this.#delayLength);
        const now = Date.now();
        if (now - this.#triggerStartTime >= this.#delayLength * 0.95) {
            this.#triggerStartTime = now;
            this.#action();
        }
    }
}
