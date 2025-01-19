using Microsoft.Win32;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Model.DataSeries.Heatmap2DArrayDataSeries;
using SciChart.Charting.Visuals.Axes;
using SciChart.Charting.Visuals.RenderableSeries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MathNet.Numerics.IntegralTransforms;
using SciChart.Charting.Model;
using System.Windows.Media;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private bool _run = true;
        private const int HeatmapWidth = 1024;
        private const int HeatmapHeight = 100;
        private readonly double[,] _heatmapData = new double[HeatmapWidth, HeatmapHeight];
        private int _currentRow = 0;
        private readonly List<(double, List<double>)> _data = new();

        public MainWindow()
        {
            InitializeComponent();
            InitializeSciChart();
        }

        public class PythonInputData
        {
            public double Overall { get; set; }
            public List<double[]> Spec { get; set; } = new List<double[]>();
        }

        private void InitializeSciChart()
        {
            ConfigureAxis(sciChartSurface.XAxes, "Frequency (Hz)", AxisAlignment.Bottom);
            ConfigureAxis(sciChartSurface.YAxes, "Amplitude (dB SPL)", AxisAlignment.Left);
            ConfigureAxis(heatmapSurface.XAxes, "Time", AxisAlignment.Bottom);
            ConfigureAxis(heatmapSurface.YAxes, "Frequency (Hz)", AxisAlignment.Left);
        }

        private void ConfigureAxis(AxisCollection axes, string title, AxisAlignment alignment)
        {
            axes.Add(new NumericAxis
            {
                AxisTitle = title,
                AxisAlignment = alignment
            });
        }

        private void btStart_Click(object sender, RoutedEventArgs e)
        {
            LoadFile();
            StartListenerAsync();
        }

        private void LoadFile()
        {
            var file = lbFilePath.Text;

            using (var reader = new StreamReader(file))
            {
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    var fields = line.Split(';');
                    if (fields.Length >= 2 && double.TryParse(fields[0], out double time))
                    {
                        var datas = fields.Skip(1).Select(double.Parse).ToList();
                        _data.Add((time, datas));
                    }
                }
            }
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
                            CommandStartSpc2(socket);
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

        private void CommandStartSpc2(RequestSocket socket)
        {
            var newData = PreparePythonInputData();
            var serializedData = JsonConvert.SerializeObject(newData);
            socket.SendFrame(serializedData);

            var response = socket.ReceiveFrameString();
            ProcessResponse(response);

            var (fftResults, avgFftDbSplList) = CalculateFft(newData);

            UpdateUiWithFftResults(avgFftDbSplList);
        }

        private PythonInputData PreparePythonInputData()
        {
            var newData = new PythonInputData { Overall = 60 };
            foreach (var data in _data)
            {
                newData.Spec.Add(data.Item2.ToArray());
            }
            return newData;
        }

        private (List<Complex[]>, List<double[]>) CalculateFft(PythonInputData newData)
        {
            int lengthData = _data.Count;
            int sizeBlock = 8192;

            var fftResults = new List<Complex[]>();
            var avgFftDbSplList = new List<double[]>();

            for (int channel = 0; channel < newData.Spec[0].Length; channel++)
            {
                var (fftResult, avgFftDbSpl) = CalculateChannelFft(newData, lengthData, sizeBlock, channel);
                fftResults.Add(fftResult);
                avgFftDbSplList.Add(avgFftDbSpl);
            }

            return (fftResults, avgFftDbSplList);
        }

        private (Complex[], double[]) CalculateChannelFft(PythonInputData newData, int lengthData, int sizeBlock, int channel)
        {
            Complex[] fftResult = new Complex[sizeBlock];
            double[] avgFftDbSpl = new double[sizeBlock / 2];

            int ii = 0;
            while (ii + sizeBlock < lengthData)
            {
                if (ii + sizeBlock >= lengthData) break;

                for (int i = 0; i < sizeBlock; i++)
                {
                    fftResult[i] = new Complex(newData.Spec[i + ii][channel], 0);
                }
                Fourier.Forward(fftResult, FourierOptions.Matlab);

                for (int i = 0; i < avgFftDbSpl.Length; i++)
                {
                    avgFftDbSpl[i] = 20 * Math.Log10(fftResult[i].Magnitude/0.00002);
                }

                UpdateHeatmapData(avgFftDbSpl);
                ii += sizeBlock;
            }

            return (fftResult, avgFftDbSpl);
        }

        private void UpdateHeatmapData(double[] avgFftDbSpl)
        {
            for (int i = 0; i < avgFftDbSpl.Length; i++)
            {
                if (i < _heatmapData.GetLength(0)) // Ensure index is within bounds
                {
                    _heatmapData[i, _currentRow] = avgFftDbSpl[i];
                }
            }
            _currentRow = (_currentRow + 1) % HeatmapHeight;
        }

        private void UpdateUiWithFftResults(List<double[]> avgFftDbSplList)
        {
            Dispatcher.Invoke(() =>
            {
                sciChartSurface.RenderableSeries.Clear();
                var signalColors = (Color[])FindResource("SignalColors");

                for (int i = 0; i < avgFftDbSplList.Count; i++)
                {
                    var avgFftDbSpl = avgFftDbSplList[i];
                    var lineSeries = new XyDataSeries<double, double>();
                    for (int j = 0; j < avgFftDbSpl.Length; j++)
                    {
                        lineSeries.Append(j, avgFftDbSpl[j]);
                    }

                    var lineRenderableSeries = new FastLineRenderableSeries
                    {
                        DataSeries = lineSeries,
                        Stroke = signalColors[i % signalColors.Length]
                    };

                    sciChartSurface.RenderableSeries.Add(lineRenderableSeries);
                }

                var heatmapDataSeries = new UniformHeatmapDataSeries<double, double, double>(_heatmapData, 0, 1, 0, 1);
                heatmapSeries.DataSeries = heatmapDataSeries;
            });
        }

        private void ProcessResponse(string response)
        {
            if (response.StartsWith("VALUE"))
            {
                var errors = JsonConvert.DeserializeObject<List<double>>(response.Substring(6));
                if (errors != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        lbSPL.Content = $"Errors: {string.Join(", ", errors.Select(e => $"{Math.Round(e)} dB"))}";
                    });
                }
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    lbSPL.Content = "Error: Invalid response";
                });
            }
        }

        private void CommandShowValue(RequestSocket socket)
        {
            var message = socket.ReceiveFrameString();
            if (message.StartsWith("VALUE"))
            {
                var errors = JsonConvert.DeserializeObject<List<double>>(message.Substring(6));
                if (errors != null)
                {
                    lbSPL.Content = $"Errors: {string.Join(", ", errors.Select(e => $"{Math.Round(e)} dB"))}";
                }
                else
                {
                    lbSPL.Content = "Error: Invalid data";
                }
            }
            else
            {
                lbSPL.Content = "Error: Invalid data";
            }
        }

        private void LoadCsv_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                lbFilePath.Text = openFileDialog.FileName;
                LoadFile();
            }
        }
    }
}
