import { IPlayerData, IBattlerAttribute, EAttribute } from "../Shared/Api/Types";
import { BattleAttributes } from "../Battle/BattleAttributes";
import { formatNum, normalizeText } from "../Shared/GlobalFunctions";
import { GameManager } from "./GameManager";
import { DataManager } from "./DataManager";

export class AttributeManager {
    static #attTableLabels = document.getElementById("attTableLabels") as HTMLDivElement;
    static #attTableButtons = document.getElementById("attTableButtons") as HTMLDivElement;
    static #attTableNumbers = document.getElementById("attTableNumbers") as HTMLDivElement;
    static #attributePointsValue = document.getElementById("attributePointsValue") as HTMLSpanElement;
    static #totalAttributesList = document.getElementById("totalAttributesList") as HTMLUListElement;
    static #changedAttributes: number[];
    static #availablePoints: number;

    static init(playerData: IPlayerData, equipmentAttributes: IBattlerAttribute[]) {
        this.#resetChangedAtts();
        this.createCoreAttributesUi(playerData);
        this.createAllAttributesUI(playerData, equipmentAttributes);
    }

    static createAllAttributesUI(playerData: IPlayerData, equipmentAttributes: IBattlerAttribute[]) {
        const allAtts = new BattleAttributes([...playerData.attributes, ...equipmentAttributes]);
        const listItems = allAtts.getAttributeMap(true).map(att => {
            let listItem = document.createElement("li");
            listItem.textContent = normalizeText(att.name) + ": " + formatNum(att.value);
            return listItem;
        });
        this.#totalAttributesList.replaceChildren(...listItems);
    }

    static createCoreAttributesUi(playerData: IPlayerData) {
        const attributeData = DataManager.attributes;
        this.#availablePoints = playerData.statPointsGained - playerData.statPointsUsed;
        this.#attributePointsValue.textContent = this.#availablePoints.toString();
        playerData.attributes
            .filter(this.isCoreAttribute) 
            .sort((a, b) => a.attributeId - b.attributeId)
            .forEach(att => {
                const attData = attributeData[att.attributeId];
                const spanLabelDiv = document.createElement('div');
                const spanLabel = document.createElement('span');
                spanLabel.textContent = attData.name + ': ';
                spanLabelDiv.append(spanLabel);
                this.#attTableLabels.append(spanLabelDiv);

                const spanNumberDiv = document.createElement('div');
                const spanNumber = document.createElement('span');
                spanNumber.id = attData.name + '_number';
                spanNumber.textContent = att.amount.toString();
                spanNumberDiv.append(spanNumber);
                this.#attTableNumbers.append(spanNumberDiv);

                const buttonsDiv = document.createElement('div');
                const minusButton = document.createElement('button');
                const plusButton = document.createElement('button');
                minusButton.textContent = '-';
                minusButton.addEventListener('click', e => {
                    AttributeManager.removeAttributePoint(att.attributeId);
                })
                plusButton.textContent = '+';
                plusButton.addEventListener('click', e => {
                    AttributeManager.addAttributePoint(att.attributeId);
                })
                buttonsDiv.append(minusButton);
                buttonsDiv.append(plusButton);
                this.#attTableButtons.append(buttonsDiv);
            });
    }

    static #resetChangedAtts() {
        this.#changedAttributes = [0, 0, 0, 0, 0, 0];
    }

    static addAttributePoint(attId: number) {
        if (this.#availablePoints > 0) {
            const playerAtt = GameManager.playerData.attributes.find(att => att.attributeId === attId);
            if (playerAtt && this.isCoreAttribute(playerAtt)) {
                const attData = DataManager.attributes[attId];
                this.#changedAttributes[attId] += 1;
                this.#availablePoints -= 1;
                const numberSpan = document.getElementById(attData.name + '_number');
                if (numberSpan) {
                    numberSpan.textContent = (playerAtt.amount + this.#changedAttributes[attId]).toString();
                }
                this.#attributePointsValue.textContent = this.#availablePoints.toString();
            }
        }
    }

    static removeAttributePoint(attId: number) {
        const playerAtt = GameManager.playerData.attributes.find(att => att.attributeId === attId);
        if (playerAtt && this.isCoreAttribute(playerAtt) && playerAtt.amount + this.#changedAttributes[attId] > 1) {
            const attData = DataManager.attributes[attId];
            this.#changedAttributes[attId] -= 1;
            this.#availablePoints += 1;
            const numberSpan = document.getElementById(attData.name + '_number');
            if (numberSpan) {
                numberSpan.textContent = (playerAtt.amount + this.#changedAttributes[attId]).toString();
            }
            this.#attributePointsValue.textContent = this.#availablePoints.toString();
        }
    }

    static saveStats() {
        GameManager.updateStats(this.#changedAttributes.map((amount, id) => ({ attributeId: id, amount: amount})));
        this.#resetChangedAtts();
    }

    static isCoreAttribute(att: IBattlerAttribute) {
        const id = att.attributeId;
        return id === EAttribute.Strength
            || id === EAttribute.Endurance
            || id === EAttribute.Intellect
            || id === EAttribute.Agility
            || id === EAttribute.Dexterity
            || id === EAttribute.Luck;
    }
}