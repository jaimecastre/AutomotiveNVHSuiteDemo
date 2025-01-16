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
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using SciChart.Charting.Model.DataSeries.Heatmap2DArrayDataSeries;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private bool _run = true;
        private const int HeatmapWidth = 1024;
        private const int HeatmapHeight = 100;
        private double[,] _heatmapData = new double[HeatmapHeight, HeatmapWidth];
        private int _currentRow = 0;

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
            var xAxis = new NumericAxis
            {
                AxisTitle = "Frequency (Hz)",
                AxisAlignment = AxisAlignment.Bottom
            };

            var yAxis = new NumericAxis
            {
                AxisTitle = "Amplitude (dB SPL)",
                AxisAlignment = AxisAlignment.Left
            };

            sciChartSurface.XAxes.Add(xAxis);
            sciChartSurface.YAxes.Add(yAxis);

            var heatmapXAxis = new NumericAxis
            {
                AxisTitle = "Time",
                AxisAlignment = AxisAlignment.Bottom
            };

            var heatmapYAxis = new NumericAxis
            {
                AxisTitle = "Frequency (Hz)",
                AxisAlignment = AxisAlignment.Left
            };

            heatmapSurface.XAxes.Add(heatmapXAxis);
            heatmapSurface.YAxes.Add(heatmapYAxis);
        }

        private void btStart_Click(object sender, RoutedEventArgs e)
        {
            StartListenerAsync();
        }

        private async void StartListenerAsync()
        {
            await Task.Run(Listener);
        }

        private void Listener()
        {
            using (var socket = new RequestSocket("tcp://127.0.0.1:5555"))
            {
                socket.SendFrame("START");
                var resp = socket.ReceiveFrameString();
                if (resp != "OK")
                {
                    Console.WriteLine($"Unexpected response: {resp}");
                    return;
                }

                _run = true;
                while (_run)
                {
                    string str = resp; //socket.ReceiveFrameString();
                    switch (str)
                    {
                        case "OK":
                            CommandStartSpc(socket);
                            break;

                        case "VALUE":
                            CommandShowValue(socket);
                            break;

                        default:
                            Console.WriteLine($"Unexpected message: {str}");
                            break;
                    }

                    Thread.Sleep(1000);
                }
            }
        }

        private void CommandStartSpc(RequestSocket socket)
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
            //socket.SendFrame(serializedData);

            // Calculate FFT
            Complex[] fftResult = new Complex[newData.spec.Length];
            for (int i = 0; i < newData.spec.Length; i++)
            {
                fftResult[i] = new Complex(newData.spec[i], 0);
            }
            Fourier.Forward(fftResult, FourierOptions.Matlab);

            // Calculate average FFT in dB SPL
            double[] avgFftDbSpl = new double[fftResult.Length / 2];
            for (int i = 0; i < avgFftDbSpl.Length; i++)
            {
                avgFftDbSpl[i] = 20 * Math.Log10(fftResult[i].Magnitude);
            }

            // Plotting the average FFT in dB SPL
            var dataSeries = new XyDataSeries<double, double>();
            for (int i = 0; i < avgFftDbSpl.Length; i++)
            {
                dataSeries.Append(i, avgFftDbSpl[i]);
            }

            Dispatcher.Invoke(() =>
            {
                var lineSeries = new FastLineRenderableSeries
                {
                    DataSeries = dataSeries
                };

                sciChartSurface.RenderableSeries.Clear();
                sciChartSurface.RenderableSeries.Add(lineSeries);

                // Update heatmap data
                for (int i = 0; i < avgFftDbSpl.Length; i++)
                {
                    _heatmapData[_currentRow, i] = avgFftDbSpl[i];
                }
                _currentRow = (_currentRow + 1) % HeatmapHeight;

                var heatmapDataSeries = new UniformHeatmapDataSeries<double, double, double>(_heatmapData, 0, 1, 0, 1);
                heatmapSeries.DataSeries = heatmapDataSeries;
            });
        }

        private void CommandShowValue(RequestSocket socket)
        {
            var message = socket.ReceiveFrameString();
            if (double.TryParse(message, out double value))
            {
                lbSPL.Content = $"Error: {Math.Round(value - 100)} dB";
            }
            else
            {
                lbSPL.Content = "Error: Invalid data";
            }
        }
    }
}
