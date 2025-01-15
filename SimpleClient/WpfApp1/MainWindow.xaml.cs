using NetMQ.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NetMQ;
using Newtonsoft.Json;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.Axes;
using SciChart.Charting.Visuals.RenderableSeries;
using SciChart.Data.Model;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            InitializeSciChart();
        }

        public class PythonInputData
        {
            public double overall { get; set; }
            public double[] spec { get; set; } = new double[1024];
        }

        private void InitializeSciChart()
        {
            sciChartSurface.XAxes.Add(new NumericAxis());
            sciChartSurface.YAxes.Add(new NumericAxis());
        }

        private void btStart_Click(object sender, RoutedEventArgs e)
        {
            //Sending the data to Python
            using (var requestSocket = new RequestSocket("tcp://127.0.0.1:5555"))
            {
                var newData = new PythonInputData();
                newData.overall = 60;
                newData.spec = new double[1024];
                var random = new Random();
                for (int i = 0; i < newData.spec.Length; i++)
                {
                    newData.spec[i] = random.NextDouble();
                }

                var serializedData = JsonConvert.SerializeObject(newData);
                requestSocket.SendFrame(serializedData);

                //Receiving the data from Python
                var message = requestSocket.ReceiveFrameString();
                lbSPL.Content = "Error: " + (Math.Round(Convert.ToDouble(message) - 100)).ToString() + "dB";

                // Plotting the data
                var dataSeries = new XyDataSeries<double, double>();
                for (int i = 0; i < newData.spec.Length; i++)
                {
                    dataSeries.Append(i, newData.spec[i]);
                }

                var lineSeries = new FastLineRenderableSeries
                {
                    DataSeries = dataSeries
                };

                sciChartSurface.RenderableSeries.Clear();
                sciChartSurface.RenderableSeries.Add(lineSeries);
            }
        }
    }
}
