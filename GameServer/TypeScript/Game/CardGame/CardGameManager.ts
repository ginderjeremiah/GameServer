import { Renderable } from "../Abstract/Renderable";
import { ObjLayer } from "./ObjLayer";
import { CardHand } from "./CardHand";

export class CardGameManager {
    #cardGameCanvas;
    #context;
    #hand;
    #objLayers;
    #currHover: Renderable | undefined | null = null;
    static mousePos = { x: 0, y: 0 };

    constructor() {
        this.#objLayers = {
            background: new ObjLayer,
            card: new ObjLayer
        }
        //this.#cardGameCanvas = document.getElementById('cardGameCanvas');
        this.#cardGameCanvas = document.getElementsByTagName('canvas')[0];
        this.#cardGameCanvas.addEventListener('mousemove', (event) => {
            CardGameManager.updateMousePos(event.offsetX, event.offsetY);
        })
        this.#context = this.#cardGameCanvas.getContext('2d')!;
        this.#context.textBaseline = 'top';
        this.#context.textAlign = 'center';
        this.#hand = new CardHand(this.#objLayers.card);
        this.#hand.addCard('img/card/Placeholder.png', 'Strike', 'Deal x dmg.');
        this.#hand.addCard('img/card/Placeholder.png', 'Block', 'Block x dmg.');
        this.#hand.addCard('img/card/Placeholder.png', 'Strike', 'Deal x dmg.');
        this.#hand.addCard('img/card/Placeholder.png', 'Block', 'Block x dmg.');
        this.#hand.addCard('img/card/Placeholder.png', 'Strike', 'Deal x dmg.');
    }

    update(timeDelta: number) {
        if (this.#cardGameCanvas.offsetParent !== null) {
            this.#context.clearRect(0, 0, this.#cardGameCanvas.width, this.#cardGameCanvas.height);
            let scale = this.#cardGameCanvas.width / this.#cardGameCanvas.getBoundingClientRect().width,
                x = CardGameManager.mousePos.x * scale,
                y = CardGameManager.mousePos.y * scale,
                hitObj,
                clicked = false;

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
        } else {
            //console.log('card game not visible');
        }
    }

    static updateMousePos(x: number, y: number) {
        CardGameManager.mousePos.x = x;
        CardGameManager.mousePos.y = y;
    }
}