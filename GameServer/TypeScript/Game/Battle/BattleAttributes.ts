/// <reference path="../Shared/Api/Enums.ts"/>
class BattleAttributes {
    static #attributesMaxId = Object.values(AttributeType)[Object.values(AttributeType).length - 1] as number;
    attributes: number[];
    battlerAttributes: IBattlerAttribute[];

    constructor(attList: IBattlerAttribute[], calcDerivedStats: boolean = true) {
        this.battlerAttributes = attList.slice();
        this.attributes = new Array<number>(BattleAttributes.#attributesMaxId);
        this.resetAttributes(calcDerivedStats);
    }

    getValue(attId: number) {
        return this.attributes[attId];
    }

    resetAttributes(calcDerivedStats: boolean = true) {
        this.attributes.fill(0, 0, BattleAttributes.#attributesMaxId);
        this.battlerAttributes.forEach(att => this.attributes[att.attributeId] += att.amount);
        if (calcDerivedStats) {
            this.#calculateDerivedStats()
        }
    }

    addAttributes(attList: IBattlerAttribute[], calcDerivedStats: boolean = true) {
        this.battlerAttributes.push(...attList);
        this.resetAttributes(calcDerivedStats);
    }

    getAttributeMap(includeZeroes: boolean = false) {
        return this.attributes
            .map((att, i) => ({name: AttributeType[i], value: att}))
            .filter(att => att.value != 0 || includeZeroes);
    }

    #calculateDerivedStats() {
        this.attributes[AttributeType.MaxHealth] += 50 + (20 * this.attributes[AttributeType.Endurance]) + (5 * this.attributes[AttributeType.Strength]);
        this.attributes[AttributeType.Defense] += 2 + this.attributes[AttributeType.Endurance] + (0.5 * this.attributes[AttributeType.Agility]);
        this.attributes[AttributeType.CooldownRecovery] += (0.4 * this.attributes[AttributeType.Agility]) + (0.1 * this.attributes[AttributeType.Dexterity]);
        this.attributes[AttributeType.DropBonus] += Math.log10(this.attributes[AttributeType.Luck]);
        // this.attributes[AttributeTypes.CriticalChance] = 0;
        // this.attributes[AttributeTypes.CriticalDamage] = 0;
        // this.attributes[AttributeTypes.DodgeChance] = 0;
        // this.attributes[AttributeTypes.BlockChance] = 0;
        // this.attributes[AttributeTypes.BlockReduction] = 0;
    }
}