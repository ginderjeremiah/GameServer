interface Dict<Type> {
    [key: string]: Type
}

interface Listable<Type extends Listable<Type>> {
    lNode: ListNode<Type>
}

interface PlayerData {
    playerName: string;
    level: number;
    exp: number;
    stats: BaseStats;
    selectedSkills: number[];
    statPointsGained: number;
    statPointsUsed: number;
}

interface EnemyData {
    enemyId: number;
    enemyName: string;
    enemyDrops: ItemDrop[];
    selectedSkills: number[];
}

interface EnemyInstance {
    enemyId: number;
    enemyLevel: number;
    stats: BaseStats;
    seed: number;
}

function IsEnemyInstance(instance: EnemyInstance | any): instance is EnemyInstance {
    return instance.seed !== undefined;
}

interface ItemData {
    itemId: number;
    itemName: string;
    itemDesc: string;
    itemCategoryId: number;
}

interface InventoryItem {
    inventoryItemId: number;
    playerId: number;
    itemId: number;
    rating: number;
    equipped: boolean,
    slotId: number,
    itemMods: InventoryItemMod[]
}

interface InventoryItemMod {
    itemSlotId: number,
    itemModId: number
}

interface InventoryData {
    inventory: (InventoryItem | null)[];
    equipped: (InventoryItem | null)[];
}

interface LogPreferences extends Dict<boolean> {
    Damage: boolean;
    Debug: boolean;
    Exp: boolean;
    "Level Up": boolean;
    Inventory: boolean;
    "Enemy Defeated": boolean;
}

interface SkillData {
    skillId: number;
    skillName: string;
    baseDamage: number;
    damageMultipliers: [{
        attributeName: string,
        multiplier: number
    }];
    skillDesc: string;
    cooldownMS: number;
    iconPath: string;
}

interface ItemDrop {
    droppedById: number;
    itemId: number;
    dropRate: number;
}

interface ZoneData {
    zoneId: number;
    zoneName: string;
    zoneDesc: string;
    zoneOrder: number;
}

interface BaseStats extends Dict<number> {
    strength: number;
    endurance: number;
    intellect: number;
    agility: number;
    dexterity: number;
    luck: number;
}

interface DerivedStats {
    maxHealth: number;
    defense: number;
    cooldownRecovery: number;
    dropMod: number;
    //critChance : number;
    //critMulti : number;
    //dodge : number;
    //blockChance : number;
    //blockMulti : number;
}

interface Tag {
    tagId: number;
    tagName: string;
    tagCategory: string;
}

interface ItemMod {
    itemModId: number;
    itemModName: string;
    removable: boolean;
    itemModDesc: string;
    slotTypeId: number
}

interface ItemCategory {
    itemCategoryId: number;
    categoryName: string;
}

interface ItemSlot {
    itemSlotId: number
    itemId: number;
    slotTypeId: number;
    guaranteedId: number;
    probability: number;
}

interface DefeatResults {
    cooldown: number,
    rewards?: DefeatRewards
}

interface DefeatRewards {
    expReward: number,
    drops: InventoryItem[]
}

interface Change<T> {
    changeType: ChangeType;
    item: T;
}

interface SlotType {
    slotTypeId: number;
    slotTypeName: string;
}

interface SelOption {
    id: number;
    name: string;
}

interface InventoryUpdate {
    inventoryItemId: number,
    slotId: number
}