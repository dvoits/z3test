using Measurement;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Input;

namespace PerformanceTest.Management
{
    public partial class Scatterplot : Form
    {
        private CompareExperimentsViewModel vm = null;
        private ExperimentStatusViewModel experiment1, experiment2;
        private string category = "";
        private bool fancy = false;
        private double axisMinimum = 0.1;
        private uint axisMaximum = 1800;
        private uint errorLine = 100;
        private uint timeoutX = 1800;
        private uint timeoutY = 1800;
        private Dictionary<string, int> classes = new Dictionary<string, int>();
        public Scatterplot(CompareExperimentsViewModel vm, ExperimentStatusViewModel exp1, ExperimentStatusViewModel exp2, double timeout1, double timeout2)
        {
            InitializeComponent();
            this.vm = vm;
            this.experiment1 = exp1;
            this.experiment2 = exp2;
            this.Text = "Plot: " + exp1.ID.ToString() + " vs " + exp2.ID.ToString();
            string category1 = experiment1.Category == null ? "" : experiment1.Category;
            string category2 = experiment2.Category == null ? "" : experiment2.Category;
            category = (category1 == category2) ? category1 : category1 + " -vs- " + category2;
            timeoutX = (uint)timeout1;
            timeoutY = (uint)timeout2;
        }

        private void setupChart()
        {
            chart.Legends.Clear();
            chart.Titles.Clear();

            axisMaximum = timeoutX;
            if (timeoutY > axisMaximum) axisMaximum = timeoutY;
            // Round max up to next order of magnitude.
            {
                uint orders = 0;
                uint temp = axisMaximum;
                while (temp > 0)
                {
                    temp = temp / 10;
                    orders++;
                }

                uint newmax = 1;
                for (uint i = 0; i < orders; i++)
                    newmax *= 10;

                if (newmax <= axisMaximum)
                {
                    // errorLine = ((newmax * 10) - newmax) / 2;
                    axisMaximum *= 10;
                }
                else
                {
                    // errorLine = axisMaximum + ((newmax - axisMaximum) / 2);
                    axisMaximum = newmax;
                }

                errorLine = axisMaximum;
            }

            Title t = new Title(category, Docking.Top);
            t.Font = new Font(FontFamily.GenericSansSerif, 16.0f, FontStyle.Bold);
            chart.Titles.Add(t);
            chart.ChartAreas[0].AxisX.Title = "Experiment #" + experiment1.ID + ": " + experiment1.Note;
            chart.ChartAreas[0].AxisY.Title = "Experiment #" + experiment2.ID + ": " + experiment2.Note;
            chart.ChartAreas[0].AxisY.TextOrientation = TextOrientation.Rotated270;
            chart.ChartAreas[0].AxisX.Minimum = axisMinimum;
            chart.ChartAreas[0].AxisX.Maximum = axisMaximum;
            chart.ChartAreas[0].AxisX.IsLogarithmic = true;
            chart.ChartAreas[0].AxisY.Minimum = axisMinimum;
            chart.ChartAreas[0].AxisY.Maximum = axisMaximum;
            chart.ChartAreas[0].AxisY.IsLogarithmic = true;
            chart.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            chart.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            chart.ChartAreas[0].AxisX.MinorGrid.Enabled = true;
            chart.ChartAreas[0].AxisY.MinorGrid.Enabled = true;
            chart.ChartAreas[0].AxisX.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            chart.ChartAreas[0].AxisY.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            chart.ChartAreas[0].AxisX.MinorGrid.LineColor = Color.LightGray;
            chart.ChartAreas[0].AxisY.MinorGrid.LineColor = Color.LightGray;
            chart.ChartAreas[0].AxisX.MinorGrid.Interval = 1;
            chart.ChartAreas[0].AxisY.MinorGrid.Interval = 1;

            chart.Series.Clear();

            chart.Series.Add("Timeout Markers");
            chart.Series[0].ChartType = SeriesChartType.FastLine;
            chart.Series[0].Color = Color.Green;
            chart.Series[0].BorderDashStyle = ChartDashStyle.Dash;
            chart.Series[0].Points.AddXY(axisMinimum, timeoutY);
            chart.Series[0].Points.AddXY(timeoutX, timeoutY);
            chart.Series[0].Points.AddXY(timeoutX, axisMinimum);

            chart.Series.Add("Error Markers");
            chart.Series[1].ChartType = SeriesChartType.FastLine;
            chart.Series[1].Color = Color.Red;
            chart.Series[1].BorderDashStyle = ChartDashStyle.Dash;
            chart.Series[1].Points.AddXY(axisMinimum, errorLine);
            chart.Series[1].Points.AddXY(errorLine, errorLine);
            chart.Series[1].Points.AddXY(errorLine, axisMinimum);

            chart.Series.Add("Diagonal");
            chart.Series[2].ChartType = SeriesChartType.FastLine;
            chart.Series[2].Color = Color.Blue;
            chart.Series[2].BorderDashStyle = ChartDashStyle.Dash;
            chart.Series[2].Points.AddXY(axisMinimum, axisMinimum);
            chart.Series[2].Points.AddXY(axisMaximum, axisMaximum);

            classes.Clear();

            if (!fancy)
                addSeries("default");

            foreach (double d in new double[] { 5.0, 10.0 })
            {
                addSpeedupLine(chart, d, Color.LightBlue);
                addSpeedupLine(chart, 1.0 / d, Color.LightBlue);
            }
        }
        private void addSeries(string title)
        {
            if (fancy)
            {
                chart.Series.Add(title);
                Series newSeries = chart.Series.Last();
                int inx = chart.Series.Count - 1;
                int m3 = inx % 3;
                int d3 = inx / 3;
                newSeries.ChartType = SeriesChartType.Point;
                newSeries.MarkerStyle = MarkerStyle.Cross;
                newSeries.MarkerSize = 6;
                switch (m3)
                {
                    case 0: newSeries.MarkerColor = Color.FromArgb(0, 0, 255 / d3); break;
                    case 1: newSeries.MarkerColor = Color.FromArgb(0, 255 / d3, 0); break;
                    case 2: newSeries.MarkerColor = Color.FromArgb(255 / d3, 0, 0); break;
                }
                newSeries.XAxisType = AxisType.Primary;
                newSeries.YAxisType = AxisType.Primary;
            }
            else if (chart.Series.Count <= 3)
            {
                chart.Series.Add(title);
                Series newSeries = chart.Series.Last();
                newSeries.ChartType = SeriesChartType.FastPoint;
                newSeries.MarkerStyle = MarkerStyle.Cross;
                newSeries.MarkerSize = 6;
                newSeries.MarkerColor = Color.Blue;
                newSeries.XAxisType = AxisType.Primary;
                newSeries.YAxisType = AxisType.Primary;

                chart.Series.Add("Winners");
                newSeries = chart.Series.Last();
                newSeries.ChartType = SeriesChartType.FastPoint;
                newSeries.MarkerStyle = MarkerStyle.Cross;
                newSeries.MarkerSize = 6;
                newSeries.MarkerColor = Color.Green;
                newSeries.XAxisType = AxisType.Primary;
                newSeries.YAxisType = AxisType.Primary;

                chart.Series.Add("Losers");
                newSeries = chart.Series.Last();
                newSeries.ChartType = SeriesChartType.FastPoint;
                newSeries.MarkerStyle = MarkerStyle.Cross;
                newSeries.MarkerSize = 6;
                newSeries.MarkerColor = Color.OrangeRed;
                newSeries.XAxisType = AxisType.Primary;
                newSeries.YAxisType = AxisType.Primary;
            }
        }
        private void refreshChart()
        {
            double totalX = 0.0, totalY = 0.0;
            uint total = 0, y_faster = 0, y_slower = 0;

            try
            {
                if (vm.CompareItems != null && vm.CompareItems.Count() > 0)
                {
                    foreach(var item in vm.CompareItems)
                    {
                        double x = item.Runtime1;
                        double y = item.Runtime2;

                        if (x < axisMinimum) x = axisMinimum;
                        if (y < axisMinimum) y = axisMinimum;

                        ResultStatus rc1 = item.Status1;
                        ResultStatus rc2 = item.Status2;
                        int res1 = item.Sat1 + item.Unsat1;
                        int res2 = item.Sat2 + item.Unsat2;

                        if ((!ckSAT.Checked && (item.Sat1 > 0 || item.Sat2 > 0)) ||
                             (!ckUNSAT.Checked && (item.Unsat1 > 0 || item.Unsat2 > 0)) ||
                             (!ckUNKNOWN.Checked && ((rc1 == ResultStatus.Success && res1 == 0) || (rc2 == ResultStatus.Success && res2 == 0))) ||
                             (!ckBUG.Checked && (rc1 == ResultStatus.Bug || rc2 == ResultStatus.Bug)) ||
                             (!ckERROR.Checked && (rc1 == ResultStatus.Error || rc2 == ResultStatus.Error)) ||
                             (!ckTIME.Checked && (rc1 == ResultStatus.Timeout || rc2 == ResultStatus.Timeout)) ||
                             (!ckMEMORY.Checked && (rc1 == ResultStatus.OutOfMemory || rc2 == ResultStatus.OutOfMemory)))
                             continue;

                        if ((rc1 != ResultStatus.Success && rc1 != ResultStatus.Timeout) || (x != timeoutX && res1 == 0))
                            x = errorLine;
                        if ((rc2 != ResultStatus.Success && rc2 != ResultStatus.Timeout) || (y != timeoutY && res2 == 0))
                            y = errorLine;

                        if (x < timeoutX && y < timeoutY)
                        {
                            totalX += x;
                            totalY += y;
                        }

                        if (fancy)
                        {
                            string name = item.Filename;
                            int inx = name.IndexOf('\\', name.IndexOf('\\') + 1);
                            string c = (inx > 0) ? name.Substring(0, inx) : name;
                            Series s;

                            if (classes.ContainsKey(c))
                                s = chart.Series[classes[c]];
                            else
                            {
                                addSeries(c);
                                classes.Add(c, chart.Series.Count - 1);
                                s = chart.Series.Last();
                            }

                            s.Points.AddXY(x, y);
                            s.Points.Last().ToolTip = name;
                        }
                        else
                        {
                            if ((item.Sat1 < item.Sat2 && item.Unsat1 == item.Unsat2) ||
                               (item.Sat1 == item.Sat2 && item.Unsat1 < item.Unsat2))
                                chart.Series[4].Points.AddXY(x, y);
                            else if ((item.Sat1 > item.Sat2 && item.Unsat1 == item.Unsat2) ||
                                (item.Sat1 == item.Sat2 && item.Unsat1 > item.Unsat2))
                                chart.Series[5].Points.AddXY(x, y);
                            else
                                chart.Series[3].Points.AddXY(x, y);
                        }

                        if (x > y) y_faster++; else if (y > x) y_slower++;
                        total++;
                    };
                }
            }
            finally
            {
                chart.Update();
            }

            double avgSpeedup = totalX / totalY;
            lblAvgSpeedup.Text = avgSpeedup.ToString("N3");
            if (avgSpeedup >= 1.0)
                lblAvgSpeedup.ForeColor = Color.Green;
            else if (avgSpeedup < 1.0)
                lblAvgSpeedup.ForeColor = Color.Red;

            lblTotal.Text = total.ToString();
            lblFaster.Text = y_faster.ToString();
            lblSlower.Text = y_slower.ToString();
        }
        private void scatterTest_Load(object sender, EventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            setupChart();
            refreshChart();
            Mouse.OverrideCursor = null;
        }
        private void ckCheckedChanged(object sender, EventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            fancy = cbFancy.Checked;
            setupChart();
            refreshChart();
            Mouse.OverrideCursor = null;
        }
        private void addSpeedupLine(Chart chart, double f, Color c)
        {
            Series s = chart.Series.Add("x" + f.ToString());
            s.ChartType = SeriesChartType.FastLine;
            s.Color = c;
            s.BorderDashStyle = ChartDashStyle.Dot;
            s.Points.AddXY(axisMinimum, axisMinimum * f);
            s.Points.AddXY(axisMaximum / f, axisMaximum);
            s.Points.AddXY(axisMaximum, axisMaximum);
        }
    }
}
