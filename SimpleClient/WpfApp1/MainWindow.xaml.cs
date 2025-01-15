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

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private bool _run = true;

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
            sciChartSurface.YAxes.Add(new NumericAxis { AxisAlignment = AxisAlignment.Left });
        }

        private void btStart_Click(object sender, RoutedEventArgs e)
        {
            StartListenerAsync();

            //Sending the data to Python
            //using (var requestSocket = new RequestSocket("tcp://127.0.0.1:5555"))
            //{
                //var newData = new PythonInputData();
                //newData.overall = 60;
                //newData.spec = new double[1024];
                //var random = new Random();
                //for (int i = 0; i < newData.spec.Length; i++)
                //{
                //    newData.spec[i] = random.NextDouble();
                //}

                //var serializedData = JsonConvert.SerializeObject(newData);
                //requestSocket.SendFrame(serializedData);

                //Receiving the data from Python
                //var message = requestSocket.ReceiveFrameString();
                //lbSPL.Content = "Error: " + (Math.Round(Convert.ToDouble(message) - 100)).ToString() + "dB";

                //// Calculate FFT
                //Complex[] fftResult = new Complex[newData.spec.Length];
                //for (int i = 0; i < newData.spec.Length; i++)
                //{
                //    fftResult[i] = new Complex(newData.spec[i], 0);
                //}
                //Fourier.Forward(fftResult, FourierOptions.Matlab);

                //// Calculate average FFT in dB SPL
                //double[] avgFftDbSpl = new double[fftResult.Length / 2];
                //for (int i = 0; i < avgFftDbSpl.Length; i++)
                //{
                //    avgFftDbSpl[i] = 20 * Math.Log10(fftResult[i].Magnitude);
                //}

                //// Plotting the average FFT in dB SPL
                //var dataSeries = new XyDataSeries<double, double>();
                //for (int i = 0; i < avgFftDbSpl.Length; i++)
                //{
                //    dataSeries.Append(i, avgFftDbSpl[i]);
                //}

                //var lineSeries = new FastLineRenderableSeries
                //{
                //    DataSeries = dataSeries
                //};

                //sciChartSurface.RenderableSeries.Clear();
                //sciChartSurface.RenderableSeries.Add(lineSeries);
            //}
        }
    
    
        private async void StartListenerAsync()
        {
            await Task.Run(Listener);
        }

        private void Listener()
        {
            using (var socket = new RequestSocket())
            {
                socket.Bind("tcp://*:5555");

                socket.SendFrame("START");
                while (true)
                {
                    var resp = socket.ReceiveFrameString();
                    if (resp == "OK")
                        break;
                }

                _run = true;
                while (_run)
                {
                    string str = socket.ReceiveFrameString();
                    switch (str)
                    {
                        case "STARTSPC":
                            CommandStartSpc(socket);
                            break;

                        case "VALUE":
                            CommandShowValue(socket);
                            break;

                        //case "PLOTDATA":
                        //    CommandPlotData(socket);
                        //    break;

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
            socket.SendFrame(serializedData);

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

            var lineSeries = new FastLineRenderableSeries
            {
                DataSeries = dataSeries
            };

            sciChartSurface.RenderableSeries.Clear();
            sciChartSurface.RenderableSeries.Add(lineSeries);
        }

        private void CommandShowValue(RequestSocket socket)
        {
            var message = socket.ReceiveFrameString();
            lbSPL.Content = "Error: " + (Math.Round(Convert.ToDouble(message) - 100)).ToString() + "dB";
        }

        //private void CommandPlotData(RequestSocket socket)
        //{
        //    int length = 1024;


        //    // Calculate FFT
        //    Complex[] fftResult = new Complex[length];
        //    for (int i = 0; i < length; i++)
        //    {
        //        fftResult[i] = new Complex(newData.spec[i], 0);
        //    }
        //    Fourier.Forward(fftResult, FourierOptions.Matlab);

        //    // Calculate average FFT in dB SPL
        //    double[] avgFftDbSpl = new double[fftResult.Length / 2];
        //    for (int i = 0; i < avgFftDbSpl.Length; i++)
        //    {
        //        avgFftDbSpl[i] = 20 * Math.Log10(fftResult[i].Magnitude);
        //    }

        //    // Plotting the average FFT in dB SPL
        //    var dataSeries = new XyDataSeries<double, double>();
        //    for (int i = 0; i < avgFftDbSpl.Length; i++)
        //    {
        //        dataSeries.Append(i, avgFftDbSpl[i]);
        //    }

        //    var lineSeries = new FastLineRenderableSeries
        //    {
        //        DataSeries = dataSeries
        //    };

        //    sciChartSurface.RenderableSeries.Clear();
        //    sciChartSurface.RenderableSeries.Add(lineSeries);

        //}
    }
}
