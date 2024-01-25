class Transform {
    parameter: string;
    magnitude: number;
    progressionFunc: ProgressionFunc;
    relative: boolean;
    percentComplete: number;
    progression: number;

    constructor(parameter: string, magnitude: number, relative: boolean, progressionFunc: ProgressionFunc) {
        this.parameter = parameter;
        this.magnitude = magnitude;
        this.relative = relative;
        this.progressionFunc = progressionFunc;
        this.percentComplete = 0;
        this.progression = 0;
    }

    reset() {
        this.percentComplete = 0;
    }

    transform(obj: Renderable, percentComplete: number) {
        let progDif = this.progressionFunc(percentComplete);
        if (this.relative) {
            obj.renderInfo.data[this.parameter] += obj.renderInfo.data[this.parameter] + progDif * this.magnitude;
        } else {
            obj.renderInfo.data[this.parameter] += (this.magnitude - obj.renderInfo.data[this.parameter]) / (progDif / (1 - this.progression));
        }
        this.percentComplete = percentComplete;
        this.progression += progDif;
    }
}
