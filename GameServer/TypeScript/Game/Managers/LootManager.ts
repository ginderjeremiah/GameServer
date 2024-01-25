class LootManager {

    #items: Dict<ItemData>;

    constructor(items: Dict<ItemData>) {
        this.#items = items;
    }

    rollLoot(zoneLoot: string[], enemyLoot: string[], dropMod: number) {
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
