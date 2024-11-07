export class CardText {
    text;
    fontSize;

    constructor(text: string, fontSize: number) {
        this.text = text;
        this.fontSize = fontSize;
    }

    draw(context: CanvasRenderingContext2D, x: number, y: number, scale: number) {
        //context.font = Math.round(this.fontSize * scale) + "px sans-serif";
        context.font = this.fontSize * scale + "px sans-serif";
        context.fillText(this.text, x, y);
    }
}
