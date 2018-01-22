using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using USBBridge;
using SharpDX.DXGI;
using SharpDX;
using SharpDX.DirectWrite;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.Multimedia;

namespace CurrentMonitor
{
    class SimpleRingBuffer
    {
        private Double[] buffer;
        private int head, tail;
        private UInt32 head_timestamp;

        public SimpleRingBuffer(int capacity)
        {
            buffer = new Double[capacity];
            head = 0;
            tail = 0;
        }

        public void Push(UInt32 timestamp, Double value)
        {
            int next_tail = (tail + 1) % buffer.Length;
            if (next_tail == head)
                ;
        }
    }

    class MyCanvas : D2dControl.D2dControl
    {
        private SharpDX.DirectWrite.Factory FactoryDWrite = new SharpDX.DirectWrite.Factory();

        private TextFormat format;
        private TextLayout layout;

        private const int TopMargin = 8;
        private const int BottomMargin = 8;
        private const int LeftMargin = 8;
        private const int RightMargin = 8;

        private const int XGridInterval = 50;
        private const int YGridInterval = 50;

        public MyCanvas()
        {
            resCache.Add("RedBrush", t => new SharpDX.Direct2D1.SolidColorBrush(t, new RawColor4(1f, 0f, 0f, 1f)));

            resCache.Add("GridBrush", t => new SharpDX.Direct2D1.SolidColorBrush(t, new RawColor4(0.4f, 0.4f, 0.4f, 1.0f)));

            format = new TextFormat(FactoryDWrite, "Courier New", 16);
            layout = new TextLayout(FactoryDWrite, "SharpDX D2D1 Test", format, 400, 300);
        }

        private void DrawXLabel(RenderTarget target)
        {
            Double canvasWidth = this.Width;
            Double canvasHeight = this.Height;

            int numLabel = 
        }

        private void DrawYLabel(RenderTarget target)
        {

        }

        private void DrawXGrid(RenderTarget target)
        {
            Double canvasWidth = this.Width;
            int numXGridLine = (int)(canvasWidth / XGridInterval);

            SharpDX.Direct2D1.Brush brush = resCache["GridBrush"] as SharpDX.Direct2D1.Brush;

            
        }

        public override void Render(RenderTarget target)
        {
            target.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));

            target.DrawLine(new RawVector2(0, 0), new RawVector2(100, 100), resCache["RedBrush"] as SharpDX.Direct2D1.Brush);

            target.DrawText("SharpDX D2D1 Test", format, new RawRectangleF(0f, 0f, 100f, 100f), resCache["RedBrush"] as SharpDX.Direct2D1.Brush);
        }
    }

    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource CommunicationCanceller;
        private Thread Runner;

        public MainWindow()
        {
            InitializeComponent();

            this.Runner = new Thread(new ParameterizedThreadStart(this.CommunicationRunner));
        }

        private void UpdateChart(UInt32 timestamp, object[] samples)
        {
            /*while (SeriesCollection[0].Values.Count + samples.Length > 100)
            {
                SeriesCollection[0].Values.RemoveAt(0);
            }
            SeriesCollection[0].Values.AddRange(samples);*/
        }

        private delegate void UpdaterDelegate(UInt32 a, object[] b);

        private void CommunicationRunner(object _token)
        {
            using (WinUSBDevice device = new WinUSBDevice(new Guid("{50215a24-33bc-473e-83d9-b0215c461c7e}")))
            {
                CancellationToken token = (CancellationToken)_token;
                Byte[] buffer = new Byte[64];
                UInt32 bytesRead;

                //device.ControlTransfer()

                while (!token.IsCancellationRequested)
                {
                    bytesRead = device.ReadEndpoint(0x81, buffer, 64);

                    if (bytesRead >= 4 && bytesRead % 2 == 0)
                    {
                        BinaryReader reader = new BinaryReader(new MemoryStream(buffer));

                        UInt32 timestamp = reader.ReadUInt32();

                        object[] samples = new object[(bytesRead - 4) / 2];

                        for (int i = 0; i < samples.Length; i++)
                        {
                            Int16 val = reader.ReadInt16();
                            samples[i] = (object)((Double)val * 0.125);
                        }

                        this.Dispatcher.BeginInvoke(new UpdaterDelegate(this.UpdateChart), timestamp, samples);
                    }

                    //Thread.Sleep(100);
                    Thread.Yield();
                }
            }
        }

        private void ConnectButton_Checked(object sender, RoutedEventArgs e)
        {
            this.CommunicationCanceller = new CancellationTokenSource();
            
            this.Runner.Start(this.CommunicationCanceller.Token);
        }

        private void ConnectButton_Unchecked(object sender, RoutedEventArgs e)
        {
            this.CommunicationCanceller.Cancel();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (this.Runner.IsAlive)
            {
                this.CommunicationCanceller.Cancel();
                this.Runner.Join();
            }
        }
    }
}
