class ObjLayer {
    objList;
    dirty;

    constructor() {
        this.objList = new LinkedList<Renderable>((currData, objData) => objData.renderInfo.data.z > currData.renderInfo.data.z);
        this.dirty = false;
    }

    addObj(obj: Renderable) {
        this.objList.insertSorted(obj);
    }

    draw(context: CanvasRenderingContext2D) {
        /*if(this.dirty) {
            this.dirty = false;
            this.objList.sort();
        }*/
        this.objList.forEachFromEnd((obj) => obj.draw(context));
    }

    getHit(x: number, y: number) {
        /*if(this.dirty) {
            this.dirty = false;
            this.objList.sort();
        }*/
        return this.objList.first((obj) => obj.getHit(x, y));
    }

    update(delta: number) {
        this.objList.forEach((obj) => {
            obj.update(delta);
        })
    }
}
