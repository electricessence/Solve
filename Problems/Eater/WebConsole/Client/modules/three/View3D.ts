import { Scene, Camera, PerspectiveCamera, Renderer, WebGLRenderer, Object3D } from 'three';

export class View3D {

	camera: Camera;
	readonly scene: Scene = new Scene();
	readonly domElement: HTMLCanvasElement;

	private _state: number = 0;
	private readonly _container: HTMLElement;
	private readonly _size: { width: number, height: number } = { width: 0, height: 0 };

	constructor(
		container: string | HTMLElement,
		camera: Camera,
		private _renderer: Renderer = new WebGLRenderer({ antialias: true })) {

		if (camera == null) throw "'camera' cannot be null.";
		if (typeof container == 'string') {
			const c = document.getElementById(container);
			if (c == null) throw `Element '${container}' not found`;
			container = c;
		}

		this.camera = camera;
		this._container = container;

		this.setSize(
			container.clientWidth,
			container.clientHeight);

		container.appendChild(this.domElement = _renderer.domElement);
	}

	addObject(o?: Object3D): Object3D {
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

	setSize(width: number, height: number): boolean {

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
		if (_._state) return;
		this._state = 1;

		const render = () => {
			if (_._state !== 1) return;
			requestAnimationFrame(render);
			_.refreshSize();
			_.render();
		}

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

export function create(
	container: string | HTMLElement,
	camera: Camera,
	renderer?: Renderer): View3D {
	return new View3D(container, camera, renderer);
}

export default create;