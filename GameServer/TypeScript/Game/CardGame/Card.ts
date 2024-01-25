class Card extends Renderable {
    //renderInfo! : RenderInfo;
    cardImage: CardImage;
    titleText: CardText;
    descriptionText: CardText;
    cardBaseImg: HTMLImageElement;
    highlightedCardBaseImg: HTMLImageElement;
    hover: boolean;
    clicked: boolean;
    //lNode! : ListNode<Renderable>;
    animationSequences;

    constructor(imgPath: string, title: string, description: string/*, renderInfo : RenderInfo*/) {
        super();
        this.hover = false;
        this.clicked = false;
        this.cardBaseImg = new Image();
        this.cardBaseImg.src = 'img/card/CardBase.png';
        this.highlightedCardBaseImg = new Image();
        this.highlightedCardBaseImg.src = 'img/card/HighlightedCardBase.png';
        this.cardImage = new CardImage(this, imgPath);
        this.titleText = new CardText(title, 50);
        this.descriptionText = new CardText(description, 30);
        this.animationSequences = new LinkedList<AnimationSequence>((x, y) => true);
        //this.renderInfo = renderInfo;
    }

    draw(context: CanvasRenderingContext2D): void {
        let width = this.renderInfo.data.width * this.renderInfo.data.scale;
        let height = this.renderInfo.data.height * this.renderInfo.data.scale;
        let x = this.renderInfo.data.x - width / 2;
        let y = this.renderInfo.data.y - height / 2;

        context.save();
        context.translate(this.renderInfo.data.x, this.renderInfo.data.y);
        context.rotate((this.renderInfo.data.rotation) * Math.PI / 180);
        context.translate(-this.renderInfo.data.x, -this.renderInfo.data.y);

        if (this.hover) {
            context.drawImage(this.highlightedCardBaseImg, x - 10 * this.renderInfo.data.scale, y - 10 * this.renderInfo.data.scale, width + 20 * this.renderInfo.data.scale, height + 20 * this.renderInfo.data.scale);
        } else {
            context.drawImage(this.cardBaseImg, x, y, width, height);
        }
        this.cardImage.draw(context, x + 20 * this.renderInfo.data.scale, y + 80 * this.renderInfo.data.scale);
        this.titleText.draw(context, this.renderInfo.data.x, y + 20 * this.renderInfo.data.scale, this.renderInfo.data.scale);
        this.descriptionText.draw(context, this.renderInfo.data.x, y + 280 * this.renderInfo.data.scale, this.renderInfo.data.scale);

        context.restore();
    }

    getHit(x: number, y: number): boolean {
        //TODO factor in rotation
        return this.renderInfo.data.hitTop < y && this.renderInfo.data.hitLeft < x && this.renderInfo.data.hitRight > x && this.renderInfo.data.hitBottom > y;
    }

    updateRenderInfo(renderInfo: RenderInfo): void {
        this.renderInfo = renderInfo;
    }

    addAnimationSequence(animSequence: AnimationSequence): void {
        this.animationSequences.appendData(animSequence);
    }

    update(delta: number): void {
        this.animationSequences.forEach((animSeq) => {
            if (animSeq.calcAnimChange(delta)) {
                animSeq.lNode.removeSelf();
            }
        })
    }

    onHover(): boolean {
        this.hover = true;
        this.renderInfo.onHoverExtension?.(this);
        return true;
    }

    exitHover(): boolean {
        this.hover = false;
        this.renderInfo.exitHoverExtension?.(this);
        return false;
    }
}
