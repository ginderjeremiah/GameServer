/// <reference path="../Shared/Enums.ts"/>
class BattleAttributes {
    static #attributesMaxId = AttributeType.__LAST - 1;
    attributes: number[];

    constructor(attList: {attributeId: number, amount: number}[]) {
        this.attributes = new Array<number>(BattleAttributes.#attributesMaxId);
        this.attributes.fill(0, 0, BattleAttributes.#attributesMaxId);
        attList.forEach(att => this.attributes[att.attributeId] = att.amount);
        this.#calculateDerivedStats()
    }

    getValue(attId: number) {
        return this.attributes[attId];
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