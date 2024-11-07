import { ListNode } from "../Shared/ListNode";
import { Listable } from "../Shared/GlobalInterfaces";
import { RenderInfo } from "../CardGame/RenderInfo";
import { AnimationSequence } from "../CardGame/AnimationSequence";

export abstract class Renderable implements Listable<Renderable> {
    lNode!: ListNode<Renderable>;
    renderInfo!: RenderInfo;
    hover: boolean = false;
    clicked: boolean = false;
    abstract draw(context: CanvasRenderingContext2D): void;
    abstract getHit(x: number, y: number): boolean;
    abstract update(delta: number): void;
    abstract onHover(): boolean;
    abstract exitHover(): boolean;
    abstract addAnimationSequence(animSequence: AnimationSequence): void
}