/// <reference path="../Shared/enums.ts"/>
class BattleAttributes {
    static #attributesMaxId = AttributeTypes.__LAST - 1;
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
        this.attributes[AttributeTypes.MaxHealth] += 50 + (20 * this.attributes[AttributeTypes.Endurance]) + (5 * this.attributes[AttributeTypes.Strength]);
        this.attributes[AttributeTypes.Defense] += 2 + this.attributes[AttributeTypes.Endurance] + (0.5 * this.attributes[AttributeTypes.Agility]);
        this.attributes[AttributeTypes.CooldownRecovery] += (0.4 * this.attributes[AttributeTypes.Agility]) + (0.1 * this.attributes[AttributeTypes.Dexterity]);
        this.attributes[AttributeTypes.DropBonus] += Math.log10(this.attributes[AttributeTypes.Luck]);
        // this.attributes[AttributeTypes.CriticalChance] = 0;
        // this.attributes[AttributeTypes.CriticalDamage] = 0;
        // this.attributes[AttributeTypes.DodgeChance] = 0;
        // this.attributes[AttributeTypes.BlockChance] = 0;
        // this.attributes[AttributeTypes.BlockReduction] = 0;
    }
}