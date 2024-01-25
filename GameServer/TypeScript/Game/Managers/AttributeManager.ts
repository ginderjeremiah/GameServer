class AttributeManager {

    static #attTableLabels = document.getElementById("attTableLabels") as HTMLDivElement;
    static #attTableButtons = document.getElementById("attTableButtons") as HTMLDivElement;
    static #attTableNumbers = document.getElementById("attTableNumbers") as HTMLDivElement;
    static #statPointsValue = document.getElementById("statPointsValue") as HTMLSpanElement;
    static #changedStats: BaseStats;
    static #availablePoints: number;

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
            })
            plusButton.textContent = '+';
            plusButton.addEventListener('click', e => {
                AttributeManager.addStat(key);
            })
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
        }
    }

    static addStat(stat: string) {
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

    static removeStat(stat: string) {
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