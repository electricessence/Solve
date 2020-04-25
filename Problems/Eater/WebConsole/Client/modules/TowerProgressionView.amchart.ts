/* Imports */
import * as am4core from "@amcharts/amcharts4/core";
import * as am4charts from "@amcharts/amcharts4/charts";
import DATA from './amchart.sample-data';

let chart: am4charts.XYChart;

interface GenomeFitness {
	hash: string;
	level: number;
	ranking: number;
	rankingValue: number;
	size: number;
	dead: boolean;
	color: string;
}

export function init(chartContainerId: string) {

	dispose();

	chart = am4core.create(chartContainerId, am4charts.XYChart);

	let valueAxisX = chart.xAxes.push(new am4charts.ValueAxis());
	valueAxisX.minX = 0;
	valueAxisX.minWidth = 10;
	valueAxisX.renderer.ticks.template.disabled = true;
	valueAxisX.renderer.axisFills.template.disabled = true;

	let valueAxisY = chart.yAxes.push(new am4charts.ValueAxis());
	valueAxisY.minHeight = 100;
	valueAxisY.renderer.ticks.template.disabled = true;
	valueAxisY.renderer.axisFills.template.disabled = true;

	let series = chart.series.push(new am4charts.LineSeries());
	series.dataFields.valueX = "level";
	series.dataFields.valueY = "ranking";
	series.dataFields.value = "size";
	series.dataFields.hidden = "dead";
	series.strokeOpacity = 0;
	//series.interpolationDuration = 1000;
	//series.sequencedInterpolation = true;
	series.tooltip!.pointerOrientation = "vertical";

	let bullet = series.bullets.push(new am4core.Circle());
	bullet.fill = am4core.color("#ff0000");
	bullet.propertyFields.fill = "color";
	bullet.strokeOpacity = 0;
	bullet.strokeWidth = 2;
	bullet.fillOpacity = 0.5;
	bullet.stroke = am4core.color("#ffffff");
	bullet.hiddenState.properties.opacity = 0;
	bullet.tooltipText = "[bold]{hash}:[/]\Level: {valueX.value}\nRanking:{valueY.value}";

	let outline = chart.plotContainer.createChild(am4core.Circle);
	outline.fillOpacity = 0;
	outline.strokeOpacity = 0.8;
	outline.stroke = am4core.color("#ff0000");
	outline.strokeWidth = 2;
	outline.hide(0);

	let blurFilter = new am4core.BlurFilter();
	outline.filters.push(blurFilter);

	bullet.events.on("over", function (event) {
		let target = event.target;
		outline.radius = target.pixelRadius + 2;
		outline.x = target.pixelX;
		outline.y = target.pixelY;
		outline.show();
	})

	bullet.events.on("out", function (event) {
		outline.hide();
	})

	let hoverState = bullet.states.create("hover");
	hoverState.properties.fillOpacity = 1;
	hoverState.properties.strokeOpacity = 1;

	series.heatRules.push({ target: bullet, min: 2, max: 60, property: "radius" });

	bullet.adapter.add("tooltipY", function (tooltipY, target) {
		return -target.radius;
	})

	chart.cursor = new am4charts.XYCursor();
	chart.cursor.behavior = "zoomXY";
	chart.cursor.snapToSeries = series;

	chart.scrollbarX = new am4core.Scrollbar();
	chart.scrollbarY = new am4core.Scrollbar();

	intervalId = setInterval(update, 1);
}

let intervalId: NodeJS.Timeout;
const poolSize = 10;
const maxLoss = 5;
const lossInc = 1 / maxLoss;

let newIndex = 0;
function update() {
	// simulate behavior.
	const newEl: GenomeFitness = injest({
		hash: (newIndex++) + "",
		level: 0,
		ranking: 0,
		rankingValue: 0,
		size: 0.3 * Math.random() + 0.2,
		dead: false,
		color: randomColor()
	});
	chart.addData(newEl);
	const data: GenomeFitness[] = chart.data;
	const levels = groupBy(data, item => Math.floor(item.level));
	for (var key of Object.keys(levels)) {
		const level = Number(key);
		let a = levels[level];
		if (!a) {
			continue;
		}
		if (level == -1) continue;
		const nextLevel = level + 1;

		a.sort((x, y) => x.rankingValue - y.rankingValue); // For display we need to sort every time.
		let len = a.length;
		if (len == 1) a[0].ranking = 0;
		else if (len > 1) {

			let mid = Math.ceil(len / 2);

			if (len >= poolSize) {
				while (a.length > mid) {
					promote(a.pop()!);
				}
				a = a.filter(e => {
					e.level += lossInc;
					if (e.level < nextLevel) return true;
					e.level = -1;
					e.dead = true;
					return false;
				});
				len = a.length;
				mid = Math.ceil(len);
			}

			if (len == 1) a[0].ranking = 0;
			else {
				for (var i = 0; i < len; i++) {
					a[i].ranking = i < mid ? (i - mid) : (i - mid + 1);
				}
			}
		}
	}

	chart.validateRawData();
}

function injest(e: GenomeFitness) {
	e.ranking = 0;
	e.rankingValue = Math.random() * 100;
	return e;
}

function promote(e: GenomeFitness) {
	injest(e);
	e.level = Math.floor(e.level + 1);
	return e;
}

export function dispose() {
	clearInterval(intervalId);
	const c = chart;
	if (c) {
		chart = undefined!;
		c.dispose();
	}
}

interface StringMap<T> {
	[key: string]: T[]
}

interface NumberMap<T> {
	[key: number]: T[]
}


interface StringSelector<T> {
	(item:T):string
}

interface NumberSelector<T> {
	(item: T): number
}


function groupBy<T>(xs: T[], selector: StringSelector<T>): StringMap<T>
function groupBy<T>(xs: T[], selector: NumberSelector<T>): NumberMap<T>
function groupBy<T>(xs: T[], selector: Function): any {
	return xs.reduce((rv, x) => {
		//@ts-ignore;
		(rv[selector(x)] = rv[selector(x)] || []).push(x);
		return rv;
	}, {});
}

function nextInt(max: number) {
	return Math.floor(Math.random() * max);
}

function selectRandom<T>(a: T[]): T {
	return a[nextInt(a.length)];
}

function randomColor():string {
	let start = "#";
	for (let i = 0; i < 6; i++)
		start += nextInt(5) + 4;
	return start;
}