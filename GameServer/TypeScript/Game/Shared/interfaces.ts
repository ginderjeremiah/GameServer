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
    attributes: PlayerAttribute[];
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

interface NewEnemy {
    cooldown: number,
    enemyInstance: EnemyInstance
}

interface EnemyInstance {
    enemyId: number;
    enemyLevel: number;
    attributes: {attributeId: number, amount: number}[];
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
    damageMultipliers: AttributeMultiplier[];
    skillDesc: string;
    cooldownMS: number;
    iconPath: string;
}

interface AttributeMultiplier {
    attributeId: number;
    multiplier: number;
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

interface PlayerAttribute {
    playerId: number,
    attributeId: number,
    amount: number
}

interface Tag {
    tagId: number;
    tagName: string;
    tagCategory: string;
}

interface ItemModData {
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
    inventoryItemId: number;
    slotId: number;
    equipped: boolean;
}

interface AttributeUpdate {
    attributeId: number,
    amount: number
}

interface AttributeData {
    attributeId: number,
    attributeName: string,
    attributeDesc: string
}