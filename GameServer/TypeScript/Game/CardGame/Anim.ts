class Anim {
    duration: number;
    transformData: Dict<number>;
    //cancelCondition;
    remainingDuration: number;
    relative: boolean;
    dirty: boolean;

    constructor(duration: number, transformData: Dict<number>, relative: boolean) {
        this.duration = duration;
        this.remainingDuration = duration;
        this.transformData = transformData;
        this.relative = relative;
        this.dirty = 'z' in transformData;
    }

    calcAnimChange(delta: number, obj: Renderable): number {

        /*if(this.duration) {
            if(this.duration === this.remainingDuration) {
                this.transformData.xDelta = (this.transformData.x - obj.renderInfo.x) / this.duration;
                this.transformData.yDelta = (this.transformData.y - obj.renderInfo.y) / this.duration;
                this.transformData.rotDelta = (this.transformData.rotation - obj.renderInfo.rotation) / this.duration;
                this.transformData.scaleDelta = (this.transformData.scale - obj.renderInfo.scale) / this.duration;
            }
            if(this.remainingDuration > delta) {
                obj.renderInfo.x += this.transformData.xDelta * delta;
                obj.renderInfo.y += this.transformData.yDelta * delta;
                obj.renderInfo.rotation += this.transformData.rotation * delta;
                obj.renderInfo.scale += this.transformData.scaleDelta * delta;
                this.remainingDuration -= delta;
                return 0;
            } else {
                obj.renderInfo.x += this.transformData.xDelta * this.remainingDuration;
                obj.renderInfo.y += this.transformData.yDelta * this.remainingDuration;
                obj.renderInfo.rotation += this.transformData.rotation * this.remainingDuration;
                obj.renderInfo.scale += this.transformData.scaleDelta * this.remainingDuration;
                return delta - this.remainingDuration;
            }
        } else {
            obj.renderInfo.x = this.transformData.x;
            obj.renderInfo.y = this.transformData.y;
            obj.renderInfo.z = this.transformData.z;
            obj.renderInfo.rotation = this.transformData.rotation;
            obj.renderInfo.scale += this.transformData.scaleDelta;
            return delta;
        }*/

        if (this.duration) {
            if (this.remainingDuration > delta) {
                let progress = this.relative ? delta / this.duration : delta / this.remainingDuration;
                for (let key in this.transformData) {
                    obj.renderInfo.data[key] += this.relative ? this.transformData[key] * progress : (this.transformData[key] - obj.renderInfo.data[key]) * progress;
                }
                this.remainingDuration -= delta;
                return 0;
            } else {
                let remainder = this.remainingDuration / this.duration;
                for (let key in this.transformData) {
                    obj.renderInfo.data[key] += this.relative ? this.transformData[key] * remainder : this.transformData[key] - obj.renderInfo.data[key];
                }
                return delta - this.remainingDuration;
            }
        } else {
            for (let key in this.transformData) {
                obj.renderInfo.data[key] += this.relative ? this.transformData[key] : this.transformData[key] - obj.renderInfo.data[key];
            }
            return delta;
        }
    }
}
