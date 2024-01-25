class CardHand {
    cards: Card[];
    cardLayer: ObjLayer;

    renderInfo = [[],
    [{ x: 960, y: 1000, rotation: 0, z: 1 }],
    [{ x: 900, y: 1005, rotation: -3, z: 1 }, { x: 1020, y: 1005, rotation: 3, z: 2 }],
    [{ x: 840, y: 1007, rotation: -5, z: 1 }, { x: 960, y: 1000, rotation: 0, z: 2 }, { x: 1080, y: 1007, rotation: 5, z: 3 }],
    [{ x: 780, y: 1015, rotation: -8, z: 1 }, { x: 900, y: 1005, rotation: -3, z: 2 }, { x: 1020, y: 1005, rotation: 3, z: 3 }, { x: 1140, y: 1015, rotation: 8, z: 4 }],
    [{ x: 720, y: 1020, rotation: -10, z: 1 }, { x: 840, y: 1007, rotation: -5, z: 2 }, { x: 960, y: 1000, rotation: 0, z: 3 }, { x: 1080, y: 1007, rotation: 5, z: 4 }, { x: 1200, y: 1020, rotation: 10, z: 5 }],
    [], []];
    hitTop = 870;
    hitBottom = 1125;
    hitboxInfo = [[],
    [{ left: 885, right: 1035 }],
    [{ left: 885, right: 1035 }, { left: 885, right: 1035 }],
    [{ left: 885, right: 1035 }, { left: 885, right: 1035 }, { left: 885, right: 1035 }],
    [{ left: 885, right: 1035 }, { left: 885, right: 1035 }, { left: 885, right: 1035 }, { left: 885, right: 1035 }],
    [{ left: 625, right: 765 }, { left: 765, right: 895 }, { left: 895, right: 1025 }, { left: 1025, right: 1155 }, { left: 1155, right: 1295 }],
    [], []]
    baseWidth = 300;
    baseHeight = 500;
    scaleFactor = 0.5;

    constructor(layer: ObjLayer) {
        this.cards = [];
        this.cardLayer = layer;
    }

    /*
    drawCards(context, x, y) {
        let hovers = this.getHovers(x, y);
        context.save();
        context.translate(this.centerPoint.x, this.centerPoint.y);
        this.cards.forEach((card, i) => {
            context.save();
            context.rotate((-20 + 10 * i) * Math.PI / 180);
            card.draw(context, -150, -700 - 500, hovers[i]);
            context.restore();
        })
        context.restore();
    }

    getHovers(x, y) {
        let hovers = Array(this.cards.length).fill(false);
        for (let i = this.cards.length - 1; i >= 0; i--) {
            let rotateAngle = (20 - 10 * i) * Math.PI / 180,
                cos = Math.cos(rotateAngle),
                sin = Math.sin(rotateAngle),
                localX = x - this.centerPoint.x,
                localY = y - this.centerPoint.y,
                transformX = cos * localX - sin * localY,
                transformY = sin * localX + cos * localY;
            if (transformX > -157 && transformX < 143 && transformY < -700 && transformY > -1205) {
                hovers[i] = true;
                return hovers;
            }    
        }
        return hovers;
    }
    */

    addCard(imgPath: string, title: string, description: string) {
        let card: Card = new Card(imgPath, title, description);
        this.cards.push(card);
        this.updateCardInfo();
        this.cardLayer.addObj(card);
    }

    updateCardInfo() {
        let renderInfo = this.renderInfo[this.cards.length];
        let hitboxInfo = this.hitboxInfo[this.cards.length];
        for (let i = 0; i < this.cards.length; i++) {
            this.cards[i].updateRenderInfo(new RenderInfo(renderInfo[i].x, renderInfo[i].y, renderInfo[i].z, renderInfo[i].rotation, this.baseWidth, this.baseHeight, this.scaleFactor, this.hitTop, this.hitBottom, hitboxInfo[i].right, hitboxInfo[i].left, 0, (obj) => {
                const scale = this.scaleFactor;
                obj.addAnimationSequence(new AnimationSequence(obj, [
                    new Anim(0, {
                        //x:,
                        y: 880,
                        z: 20,
                        rotation: 0,
                        scale: scale * 1.5
                    }, false),
                    new Anim(200, {
                        y: -20
                    }, true)
                ], (obj) => {
                    return !obj.hover;
                }));
            }, (obj) => {
                const y = renderInfo[i].y;
                const z = renderInfo[i].z;
                const rot = renderInfo[i].rotation;
                const scale = this.scaleFactor;
                obj.addAnimationSequence(new AnimationSequence(obj, [
                    new Anim(0, {
                        //x:,
                        y: y - 20,
                        z: z,
                        rotation: rot,
                        scale: scale
                    }, false),
                    new Anim(200, {
                        y: y
                    }, false)
                ], (obj) => {
                    return obj.hover;
                }));
            }));
        }
    }
}
