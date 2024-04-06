class AttributeManager {

    static #attTableLabels = document.getElementById("attTableLabels") as HTMLDivElement;
    static #attTableButtons = document.getElementById("attTableButtons") as HTMLDivElement;
    static #attTableNumbers = document.getElementById("attTableNumbers") as HTMLDivElement;
    static #statPointsValue = document.getElementById("statPointsValue") as HTMLSpanElement;
    static #changedStats: number[];
    static #availablePoints: number;

    static init() {
        //figure out best way to sync player data... maybe create class where all instances are backed by a single data object?
        //display derived stats here?
        const playerData = GameManager.getPlayerData();
        const attributeData = DataManager.attributes;
        this.#resetChangedStats();
        this.#availablePoints = playerData.statPointsGained - playerData.statPointsUsed;
        this.#statPointsValue.textContent = this.#availablePoints.toString();

        playerData.attributes
            .filter(this.isCoreAttribute) 
            .sort((a, b) => a.attributeId - b.attributeId)
            .forEach(att => {
                const attData = attributeData[att.attributeId];
                const spanLabelDiv = document.createElement('div');
                const spanLabel = document.createElement('span');
                spanLabel.textContent = attData.attributeName + ': ';
                spanLabelDiv.append(spanLabel);
                this.#attTableLabels.append(spanLabelDiv);

                const spanNumberDiv = document.createElement('div');
                const spanNumber = document.createElement('span');
                spanNumber.id = attData.attributeName + '_number';
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

    static #resetChangedStats() {
        this.#changedStats = [];
    }

    static addAttributePoint(attId: number) {
        if (this.#availablePoints > 0) {
            const playerAtt = GameManager.getPlayerData().attributes.find(att => att.attributeId === attId);
            if (playerAtt && this.isCoreAttribute(playerAtt)) {
                const attData = DataManager.attributes[attId];
                this.#changedStats[attId] += 1;
                this.#availablePoints -= 1;
                const numberSpan = document.getElementById(attData.attributeName + '_number');
                if (numberSpan) {
                    numberSpan.textContent = (playerAtt.amount + this.#changedStats[attId]).toString();
                }
                this.#statPointsValue.textContent = this.#availablePoints.toString();
            }
        }
    }

    static removeAttributePoint(attId: number) {
        const playerAtt = GameManager.getPlayerData().attributes.find(att => att.attributeId === attId);
        if (playerAtt && this.isCoreAttribute(playerAtt) && playerAtt.amount + this.#changedStats[attId] > 1) {
            const attData = DataManager.attributes[attId];
            this.#changedStats[attId] -= 1;
            this.#availablePoints += 1;
            const numberSpan = document.getElementById(attData.attributeName + '_number');
            if (numberSpan) {
                numberSpan.textContent = (playerAtt.amount + this.#changedStats[attId]).toString();
            }
            this.#statPointsValue.textContent = this.#availablePoints.toString();
        }
    }

    static saveStats() {
        GameManager.updateStats(this.#changedStats.map((amount, id) => ({ attributeId: id, amount: amount})));
        this.#resetChangedStats();
    }

    static isCoreAttribute(att: IBattlerAttribute) {
        const id = att.attributeId;
        return id === AttributeType.Strength
            || id === AttributeType.Endurance
            || id === AttributeType.Intellect
            || id === AttributeType.Agility
            || id === AttributeType.Dexterity
            || id === AttributeType.Luck;
    }
}