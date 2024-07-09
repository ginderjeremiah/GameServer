import { Card } from "./Card";

export class CardImage {
    card: Card;
    imgMarginTop = 80;
    imgMarginHorizontal = 20;
    aspectRatio = 16 / 9;
    img: HTMLImageElement;

    constructor(card: Card, imgPath: string) {
        this.card = card;
        this.img = new Image();
        this.img.src = imgPath;
    }

    draw(context: CanvasRenderingContext2D, x: number, y: number) {
        let width = (this.card.renderInfo.data.width - this.imgMarginHorizontal * 2) * this.card.renderInfo.data.scale;
        let height = width / this.aspectRatio;
        context.drawImage(this.img, x, y, width, height);
    }
}
