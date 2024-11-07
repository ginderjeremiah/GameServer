import { Listable } from "../Shared/GlobalInterfaces";
import { Renderable } from "../Abstract/Renderable";
import { CheckFunc } from "../Shared/CustomTypes";
import { ListNode } from "../Shared/ListNode";
import { Anim } from "./Anim";

export class AnimationSequence implements Listable<AnimationSequence> {
    obj: Renderable;
    animations: Anim[];
    cancelCondition: CheckFunc<Renderable>;
    lNode!: ListNode<AnimationSequence>;

    constructor(obj: Renderable, animations: Anim[], cancelCondition: CheckFunc<Renderable>) {
        this.obj = obj;
        this.animations = animations;
        this.cancelCondition = cancelCondition;
    }

    calcAnimChange(delta: number) {
        if (this.cancelCondition(this.obj)) {
            return delta;
        }
        let remainingDelta = delta;
        while (this.animations.length > 0 && (remainingDelta = this.animations[0].calcAnimChange(remainingDelta, this.obj))) {
            if (this.animations[0].dirty) {
                this.obj.lNode.sortSelf();
            }
            this.animations.shift();
        }
        return remainingDelta;
    }
}
