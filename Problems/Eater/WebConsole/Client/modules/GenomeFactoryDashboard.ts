import * as am4core from "@amcharts/amcharts4/core";
import * as am4charts from "@amcharts/amcharts4/charts";
import { GenomeFactoryMetricsClient } from "../controllers";

let queueStates: am4charts.PieChart3D;
const elementId = "genomeFactoryDashboard";
let intervalId: NodeJS.Timeout;
let series: am4charts.PieSeries3D;

const client = new GenomeFactoryMetricsClient();

export function init() {
	dispose();

	queueStates = am4core.create(elementId, am4charts.PieChart3D);
	queueStates.innerRadius = 100;
	queueStates.hiddenState.properties.opacity = 0; // this creates initial fade-in
	//chart.legend = new am4charts.Legend(); 
	var s = new am4charts.PieSeries3D();
	s.dataFields.value = "value";
	s.dataFields.category = "key";
	s.alignLabels = true;
	series = queueStates.series.push(s);

	update();
	intervalId = setInterval(update, 200);
}

function areSame<T>(a: T[], b: T[]): boolean {
	if (a == b) return true;
	if (a==null || b==null) return false;
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

async function update() {
	const data = await client.get();
	// possible that after delay, this was disposed.
	if (queueStates) {
		const newData = data.queueStates;
		const oldData = queueStates.data;

		if (areSame(oldData, newData)) return;
		if (!oldData || !oldData.length) {
			queueStates.data = newData;
			return;
		}
		//var map: { [key: string]: number } = {};
		//for (var e of newData) {
		//	map[e.key] = e.value;
		//}
		const dataItems = series.dataItems.values;
		const len = newData.length;
		for (let i = 0; i < newData.length; i++) {
			const n = newData[i];
			const o = oldData[i];
			const dataItem = dataItems[i];
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
}

export function dispose() {
	clearInterval(intervalId);
	var q = queueStates;
	if (q) {
		series = undefined!;
		queueStates = undefined!;
		q.dispose();
	}
}