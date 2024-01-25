class RenderInfo {
    data: {
        x: number;
        y: number;
        z: number;
        rotation: number;
        width: number;
        height: number;
        scale: number;
        hitTop: number;
        hitBottom: number;
        hitRight: number;
        hitLeft: number;
        hitRotation: number;
        [key: string]: number
    };
    onHoverExtension: Action<Renderable> | undefined;
    exitHoverExtension: Action<Renderable> | undefined;

    constructor(x: number, y: number, z: number, rotation: number, width: number, height: number, scale: number, hitTop: number, hitBottom: number, hitRight: number, hitLeft: number, hitRotation: number, onHoverExtension: Action<Renderable>, exitHoverExtension: Action<Renderable>) {
        this.data = {
            x: x,
            y: y,
            z: z,
            rotation: rotation,
            width: width,
            height: height,
            scale: scale,
            hitTop: hitTop,
            hitBottom: hitBottom,
            hitRight: hitRight,
            hitLeft: hitLeft,
            hitRotation: hitRotation
        }
        this.onHoverExtension = onHoverExtension;
        this.exitHoverExtension = exitHoverExtension;
    };
}
