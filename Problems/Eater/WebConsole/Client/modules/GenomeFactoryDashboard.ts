import * as am4core from "@amcharts/amcharts4/core";
import * as am4charts from "@amcharts/amcharts4/charts";
import theme from "@amcharts/amcharts4/themes/animated";
import { GenomeFactoryMetricsClient } from "../controllers";

am4core.useTheme(theme);

let queueStates: am4charts.PieChart3D;
const elementId = "genomeFactoryDashboard";
let intervalId: NodeJS.Timeout;

const client = new GenomeFactoryMetricsClient();

export function init()
{
	dispose();

	queueStates = am4core.create(elementId, am4charts.PieChart3D);
	queueStates.innerRadius = 100;
	queueStates.hiddenState.properties.opacity = 0; // this creates initial fade-in
	//chart.legend = new am4charts.Legend(); 
	var config = new am4charts.PieSeries3D();
	config.alignLabels = true;
	var series = queueStates.series.push(config);
	series.dataFields.value = "Value";
	series.dataFields.category = "Key";

	intervalId = setInterval(update, 1000);
}

async function update() {
	var data = await client.get();
	// possible that after delay, this was disposed.
	if (queueStates) queueStates.data = data.queueStates;
}

export function dispose() {
	clearInterval(intervalId);
	if (queueStates) {
		queueStates.dispose();
		queueStates = undefined!;
	}
}