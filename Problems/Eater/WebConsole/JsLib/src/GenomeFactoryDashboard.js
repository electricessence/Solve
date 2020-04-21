import * as am4core from "@amcharts/amcharts4/core";
import * as am4charts from "@amcharts/amcharts4/charts";
import theme from "@amcharts/amcharts4/themes/animated";
am4core.useTheme(theme);
let queueCounts;
const elementId = "genomeFactoryDashboard";
export function dispose() {
    if (queueCounts) {
        queueCounts.dispose();
        queueCounts = undefined;
    }
}
export function update(firstRender, data) {
    if (firstRender) {
        dispose();
        queueCounts = am4core.create(elementId, am4charts.PieChart3D);
        queueCounts.innerRadius = 100;
        queueCounts.hiddenState.properties.opacity = 0; // this creates initial fade-in
        //chart.legend = new am4charts.Legend(); 
        var config = new am4charts.PieSeries3D();
        config.alignLabels = true;
        var series = queueCounts.series.push(config);
        series.dataFields.value = "Value";
        series.dataFields.category = "Key";
    }
    queueCounts.data = data;
}
//# sourceMappingURL=GenomeFactoryDashboard.js.map