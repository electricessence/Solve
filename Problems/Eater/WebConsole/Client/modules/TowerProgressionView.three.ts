import tweenFactory from '@tsdotnet/tween-factory';
import * as easing from '@tsdotnet/tween-factory/dist/easing';
import {AxesHelper, GridHelper, Mesh, PerspectiveCamera, PointLight} from 'three';
import {OrbitControls} from 'three/examples/jsm/controls/OrbitControls';
import {addSphereTo, ColorParam} from './three/Utils';
import createView, {View3D} from './three/View3D';
import DataService from './TowerProgression.Service';
import {simulate} from "./TowerProgression.Service.Simulator";

let view: View3D;
let registry: Record<string, Mesh>;
let controls: OrbitControls;
let service: DataService;
let simulation: Function;

const tweenBehavior = tweenFactory(500, easing.exponential.easeOut);

export function init(container: string | HTMLElement): View3D
{

	// width:100%;height:99%;min-width:250px;min-height:250px;overflow:hidden;
	dispose();

	view = createView(container, new PerspectiveCamera(45, 1, 0.1, 20000));
	view.camera.position.set(-25, 100, 225);
	const scene = view.scene;

	//const geometry = new BoxGeometry(0.2, 0.2, 0.2);
	//const material = new MeshNormalMaterial();
	//const mesh = new Mesh(geometry, material);
	//view.addObject(mesh);

	const gridHelper = new GridHelper(1000, 200, 0x222222, 0x111111);
	gridHelper.rotateX(90 * Math.PI / 180);
	//gridHelper.position.setY(-0.2);
	scene.add(gridHelper);

	const axesHelper = new AxesHelper(1000);
	//axesHelper.scale.set(10, 10, 10);
	scene.add(axesHelper);

	const light = new PointLight(0xffffff, 2);
	light.position.set(150, 250, 150);
	scene.add(light);

	//axesHelper = new AxesHelper(10);
	//axesHelper.position.set(100, 100, 100);
	//axesHelper.rotation.set(Math.PI, 0, 0);
	//axesHelper.scale.x = -1;
	//view.addObject(axesHelper);

	controls = new OrbitControls(view.camera, view.domElement);
	controls.zoomSpeed = 0.2;

	registry = {};
	service = new DataService();
	view.onBeforeRender(update)
	view.start();

	tweenBehavior.updateOnAnimationFrame();

	simulation = simulate(service);
	return view;
}

function addSphere(color: ColorParam, x: number, y: number, z: number, radius: number, segments: number = 16, rings: number = 16)
{
	return addSphereTo(view.scene, color, x, y, z, radius, segments, rings);
}

export function dispose()
{
	if (simulation)
	{
		simulation();
		simulation = undefined!;
		registry = undefined!;
	}
	if (controls)
	{
		controls.dispose();
		controls = undefined!;
	}
	if (view)
	{
		view.dispose();
		view = undefined!;
	}
	tweenBehavior.clearInterval();
	tweenBehavior.active.cancel();
}

export function update()
{
	const xScale = 2;
	let change = service.dequeueChange();
	while (change)
	{
		const {hash, level, lossCount} = change;

		const point = registry[hash];
		if (point)
		{
			if (!change.alive)
			{
				point.visible = false;
				view.scene.remove(point);
				delete registry[hash];
			} else
			{
				tweenBehavior.tweenDeltas(point.position, {x: level * xScale, y: change.ranking, z: -lossCount});
			}
		} else if (change.alive)
		{
			const p = registry[hash] = addSphere(randomColor(), 0, 0, 0, 0.4 + Math.random() * 0.03);
			tweenBehavior.tweenDeltas(p.position, {x: level * xScale, y: change.ranking, z: -lossCount});
		}
		change = service.dequeueChange();
	}
}

function nextInt(max: number)
{
	return Math.floor(Math.random() * max);
}

function randomColor(): string
{
	let start = "#";
	for (let i = 0; i < 6; i++)
		start += nextInt(5) + 4;
	return start;
}
