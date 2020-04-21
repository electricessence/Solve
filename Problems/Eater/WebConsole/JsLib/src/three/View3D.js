import { Scene, PerspectiveCamera, WebGLRenderer, Object3D } from 'three';
export class View3D {
    constructor(container, camera, _renderer = new WebGLRenderer({ antialias: true })) {
        this._renderer = _renderer;
        this.scene = new Scene();
        this._state = 0;
        this._size = { width: 0, height: 0 };
        if (camera == null)
            throw "'camera' cannot be null.";
        if (typeof container == 'string') {
            const c = document.getElementById(container);
            if (c == null)
                throw `Element '${container}' not found`;
            container = c;
        }
        this.camera = camera;
        this._container = container;
        this.setSize(container.clientWidth, container.clientHeight);
        container.appendChild(this.domElement = _renderer.domElement);
    }
    addObject(o) {
        if (!o) {
            o = new Object3D();
            o.position.set(0, 0, 0);
        }
        this.scene.add(o);
        return o;
    }
    dispose() {
        const _ = this;
        _._state = -1;
        _._container.removeChild(_._renderer.domElement);
        _.scene.dispose();
    }
    refreshSize() {
        const _ = this, c = _._container;
        _.setSize(c.clientWidth, c.clientHeight);
    }
    setSize(width, height) {
        const _ = this;
        const size = _._size;
        let changed = false;
        if (!isNaN(width) && width !== size.width) {
            size.width = width;
            changed = true;
        }
        if (!isNaN(height) && height !== size.height) {
            size.height = height;
            changed = true;
        }
        if (changed) {
            _._renderer.setSize(size.width, size.height);
            const style = _._renderer.domElement.style;
            style.width = "100%";
            style.height = "100%";
            if (_.camera instanceof PerspectiveCamera) {
                _.camera.aspect = size.width / size.height;
                _.camera.updateProjectionMatrix();
            }
        }
        return changed;
    }
    start() {
        const _ = this;
        if (_._state)
            return;
        this._state = 1;
        const render = () => {
            if (_._state !== 1)
                return;
            requestAnimationFrame(render);
            _.refreshSize();
            _.render();
        };
        render();
    }
    stop() {
        const _ = this;
        if (_._state === 1)
            _._state = 0;
    }
    render() {
        if (this.camera)
            this._renderer.render(this.scene, this.camera);
    }
}
export function create(container, camera, renderer) {
    return new View3D(container, camera, renderer);
}
export default create;
//# sourceMappingURL=View3D.js.map