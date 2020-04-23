import createView, { View3D } from './three/View3D';
import { AxesHelper, GridHelper, PerspectiveCamera, PointLight } from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls';
import { addSphereTo, ColorParam } from './three/Utils';

let view: View3D;
let controls: OrbitControls;

export function init(container: string | HTMLElement): View3D {

	// width:100%;height:99%;min-width:250px;min-height:250px;overflow:hidden;
	dispose();

	view = createView(container, new PerspectiveCamera(45, 1, 0.1, 20000));
	view.camera.position.set(225, 225, 225);
	var scene = view.scene;

	//var geometry = new BoxGeometry(0.2, 0.2, 0.2);
	//var material = new MeshNormalMaterial();
	//var mesh = new Mesh(geometry, material);
	//view.addObject(mesh);

	var gridHelper = new GridHelper(1000, 100);
	gridHelper.position.setY(-0.2);
	scene.add(gridHelper);

	var axesHelper = new AxesHelper(1000);
	//axesHelper.scale.set(10, 10, 10);
	scene.add(axesHelper);

	var light = new PointLight(0xffffff);
	light.position.set(150, 250, 150);
	scene.add(light);

	//axesHelper = new AxesHelper(10);
	//axesHelper.position.set(100, 100, 100);
	//axesHelper.rotation.set(Math.PI, 0, 0);
	//axesHelper.scale.x = -1;
	//view.addObject(axesHelper);

	controls = new OrbitControls(view.camera, view.domElement);
	view.start();

	return view;
}

export function addSphere(color: ColorParam, x: number, y: number, z: number, radius: number, segments: number = 16, rings: number = 16) {
	return addSphereTo(view.scene, color, x, y, z, radius, segments, rings);
}

export function dispose() {
	if (controls) {
		controls.dispose();
		controls = undefined!;
	}
	if (view) {
		view.dispose();
		view = undefined!;
	}
}

//export function animate() {

//	requestAnimationFrame( animate );

//	mesh.rotation.x += 0.01;
//	mesh.rotation.y += 0.02;

//	renderer.render( scene, camera );

//}