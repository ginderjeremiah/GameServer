import { Transform } from "./Transform";

export class TransformFactory {

    static newLinearTransform(parameter: string, magnitude: number, relative: boolean): Transform {
        let t: Transform = new Transform(parameter, magnitude, relative, (percentComplete: number) => {
            return percentComplete - t.percentComplete;
        })
        return t;
    }
    /* TODO: implement the following (it's actually really fricking complicated lol):
    static newBezierTransform(parameter, magnitude, relative, x1, y1, x2, y2) {
        return new Transform(parameter, magnitude, relative, (percentComplete) => {

        })
    }*/
}
