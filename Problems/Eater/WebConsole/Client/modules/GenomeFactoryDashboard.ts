import * as am4core from "@amcharts/amcharts4/core";
import * as am4charts from "@amcharts/amcharts4/charts";
import { GenomeFactoryMetricsClient, IGenomeFactoryMetrics } from "../controllers";

let chart: am4charts.PieChart3D;
let stats: Stats;
let series: am4charts.PieSeries3D;

const client = new GenomeFactoryMetricsClient();

class Stats {

	readonly element: HTMLElement;
	constructor(elementId: string) {
		const element = document.getElementById(elementId);
		if (!element) throw "Element not found";
		this.element = element;
	}

	update(metrics:IGenomeFactoryMetrics) {
		this.element.innerHTML = `
		<ul>
			<li><span>Generate New</span> : <span class="succeded">${metrics.generateNew.succeeded}</span> / <span class="failed">${metrics.generateNew.failed}</span></li>
			<li><span>Mutation</span> : <span class="succeded">${metrics.mutation.succeeded}</span> / <span class="failed">${metrics.mutation.failed}</span></li>
			<li><span>Crossover</span> : <span class="succeded">${metrics.crossover.succeeded}</span> / <span class="failed">${metrics.crossover.failed}</span></li>
			<li><span>External</span> : <span>${metrics.externalProducerQueried}</span></li>
		</ul>`;
	}

	dispose() {
		const el = this.element;
		//@ts-ignore;
		this.element = undefined;

		if (el) el.innerHTML = '';
	}

}

export function init(chartContainerId:string, statsContainerId:string) {
	dispose();

	stats = new Stats(statsContainerId);
	chart = am4core.create(chartContainerId, am4charts.PieChart3D);
	chart.innerRadius = 100;
	chart.hiddenState.properties.opacity = 0; // this creates initial fade-in
	//chart.legend = new am4charts.Legend(); 
	var s = new am4charts.PieSeries3D();
	s.dataFields.value = "value";
	s.dataFields.category = "key";
	s.alignLabels = true;
	series = chart.series.push(s);

	update();
	intervalId = setInterval(update, 200);
	return chart;
}

let intervalId: NodeJS.Timeout;

async function update() {
	const data = await client.get();
	// possible that after delay, these were disposed.
	if (stats) stats.update(data);
	if (chart) {
		const newData = data.queueStates;
		const oldData = chart.data;

		if (areSame(oldData, newData)) return false;
		if (!oldData || !oldData.length) {
			chart.data = newData;
			return true;
		}

		const dataItems: { [key: string]: am4charts.PieSeries3DDataItem } = {};
		for (var e of series.dataItems.values) dataItems[e.category] = e;

		const len = newData.length;
		for (let i = 0; i < newData.length; i++) {
			const n = newData[i];
			const o = oldData[i];
			const dataItem = dataItems[n.key] || addDataItem(n.key);
			dataItem.value = n.value;
			// let's keep the data in-sync.
			if (o) {
				o.key = n.key;
				o.value = n.value;
			} else {
				oldData[i] = n;
			}
		}

		oldData.length = len;
		series.validateDataItems();
	}
	return true;
}

export function dispose() {
	clearInterval(intervalId);
	const c = chart;
	if (c) {
		series = undefined!;
		chart = undefined!;
		c.dispose();
	}
	const s = stats;
	if (s) {
		stats = undefined!;
		s.dispose();
	}
}

function areSame<T>(a: T[], b: T[]): boolean {
	if (a == b) return true;
	if (a == null || b == null) return false;
	const len = a.length;
	if (len != b.length) return false;
	for (let i = 0; i < len; i++) {
		const ae = a[i], be = b[i];
		if (ae == be) continue;
		if (ae == null || be == null) return false;
		if (typeof ae == 'number' && typeof be == 'number' && isNaN(ae) && isNaN(be)) continue;
		if (JSON.stringify(ae) != JSON.stringify(be)) return false;
	}
	return true;
}

function addDataItem(category: string) {
	var di = series.dataItems.create();
	di.category = category;
	return di;
}
