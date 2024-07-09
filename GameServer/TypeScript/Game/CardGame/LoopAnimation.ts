import { AnimationSequence } from "./AnimationSequence";

export class LoopAnimation extends AnimationSequence {
    currentAnim = 0;

    calcAnimChange(delta: number) {
        if (this.cancelCondition(this.obj)) {
            return delta;
        }
        let remainingDelta = delta;
        while (remainingDelta = this.animations[this.currentAnim].calcAnimChange(remainingDelta, this.obj)) {
            if (this.animations[this.currentAnim].dirty) {
                this.obj.lNode.sortSelf();
            }
            this.currentAnim = this.currentAnim + 1 % this.animations.length;
        }
        return remainingDelta;
    }
}
