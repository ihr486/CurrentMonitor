using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using USBBridge;
using SharpDX.DirectWrite;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Windows.Controls.Primitives;
using System.Linq;
using System.ComponentModel;

namespace CurrentMonitor
{
    class SimpleRingBuffer
    {
        private Double[] buffer;
        private int head;
        private UInt32 head_timestamp;

        public SimpleRingBuffer(int capacity)
        {
            buffer = new Double[capacity];
            head = 0;
            head_timestamp = 0;

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = Double.NaN;
        }

        public void Push(Double value, UInt32 timestamp)
        {
            if (timestamp <= head_timestamp)
            {
                Flush();
            }
            else
            {
                for (UInt32 t = 0; head_timestamp + t < timestamp - 1 && t < buffer.Length; t++)
                {
                    buffer[head] = Double.NaN;
                    head = (head + 1) % buffer.Length;
                }
                head_timestamp = timestamp;
            }

            buffer[head] = value;
            head = (head + 1) % buffer.Length;
        }

        public void Flush()
        {
            head = 0;

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = Double.NaN;
        }

        public Double this[int i]
        {
            get
            {
                return buffer[(head + i) % buffer.Length];
            }
        }

        public int Length
        {
            get
            {
                return buffer.Length;
            }
        }

        public Double Max
        {
            get
            {
                return buffer.Max();
            }
        }

        public Double Min
        {
            get
            {
                return buffer.Min();
            }
        }
    }

    class MyCanvas : D2dControl.D2dControl
    {
        private SharpDX.DirectWrite.Factory FactoryDWrite = new SharpDX.DirectWrite.Factory();

        private float TopMargin = 8;
        private float BottomMargin = 32;
        private float LeftMargin = 8;
        private float RightMargin = 8;

        private float XGridInterval = 100;
        private float YGridInterval = 100;

        private TextFormat XLabelFormat;
        private TextFormat YLabelFormat;
        private TextFormat StatFormat;

        private RawRectangleF XLabelLayoutBox;
        private RawRectangleF YLabelLayoutBox;

        private Double MinX, MaxX;
        private Double MinY, MaxY;

        public SimpleRingBuffer Values;

        public String StatMessage;

        private RawRectangleF OffsetLayoutBox(RawRectangleF rect, float X, float Y)
        {
            return new RawRectangleF()
            {
                Top = rect.Top + Y,
                Bottom = rect.Bottom + Y,
                Left = rect.Left + X,
                Right = rect.Right + X
            };
        }

        public MyCanvas()
        {
            resCache.Add("GridBrush", t => new SharpDX.Direct2D1.SolidColorBrush(t, new RawColor4(0f, 0f, 0f, 1f)));
            resCache.Add("PlotBrush", t => new SharpDX.Direct2D1.SolidColorBrush(t, new RawColor4(0.2f, 1.0f, 0.6f, 1.0f)));

            XLabelFormat = new TextFormat(FactoryDWrite, "Courier New", 16)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Center,
                ParagraphAlignment = ParagraphAlignment.Center
            };
            YLabelFormat = new TextFormat(FactoryDWrite, "Courier New", 16)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Trailing,
                ParagraphAlignment = ParagraphAlignment.Center
            };
            StatFormat = new TextFormat(FactoryDWrite, "ＭＳ ゴシック", 16)
            {
                TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading,
                ParagraphAlignment = ParagraphAlignment.Far
            };

            FormattedText maxdigits = new FormattedText("0000000", System.Globalization.CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, new Typeface("Courier New"), 16.0, Brushes.White);

            LeftMargin += (float)maxdigits.Width;
            BottomMargin += (float)maxdigits.Height;
            RightMargin += (float)maxdigits.Width * 0.5f;
            TopMargin += (float)maxdigits.Height;

            XLabelLayoutBox = new RawRectangleF()
            {
                Top = 4.0f,
                Bottom = 4.0f + (float)maxdigits.Height,
                Left = -0.55f * (float)maxdigits.Width,
                Right = 0.55f * (float)maxdigits.Width
            };

            YLabelLayoutBox = new RawRectangleF()
            {
                Top = -1.1f * (float)maxdigits.Height,
                Bottom = 0.0f * (float)maxdigits.Height,
                Left = -4.0f - (float)maxdigits.Width,
                Right = -4.0f
            };

            MinX = 0.0;
            MaxX = 100.0;
            MinY = 0.0;
            MaxY = 100.0;

            Values = new SimpleRingBuffer(1000);
        }

        private void DrawXGrid(RenderTarget target)
        {
            RawRectangleF graphArea = new RawRectangleF(this.LeftMargin, this.TopMargin, (float)this.ActualWidth - this.RightMargin, (float)this.ActualHeight - this.BottomMargin);

            SharpDX.Direct2D1.Brush brush = resCache["GridBrush"] as SharpDX.Direct2D1.Brush;

            int numXGrid = (int)((graphArea.Right - graphArea.Left) / this.XGridInterval);

            RawVector2 lineStart = new RawVector2(graphArea.Left, graphArea.Top);
            RawVector2 lineEnd = new RawVector2(graphArea.Left, graphArea.Bottom);
            for (int x = 0; x <= numXGrid; x++)
            {
                lineStart.X = graphArea.Left + (graphArea.Right - graphArea.Left) * (float)x / (float)numXGrid;
                lineEnd.X = lineStart.X;
                target.DrawLine(lineStart, lineEnd, brush);

                String labelString = String.Format("{0:F0}", MinX + (MaxX - MinX) * (double)x / (double)numXGrid);

                target.DrawText(labelString, XLabelFormat, OffsetLayoutBox(XLabelLayoutBox, lineStart.X, lineEnd.Y), brush);
            }
        }

        private void DrawYGrid(RenderTarget target)
        {
            RawRectangleF graphArea = new RawRectangleF(this.LeftMargin, this.TopMargin, (float)this.ActualWidth - this.RightMargin, (float)this.ActualHeight - this.BottomMargin);

            SharpDX.Direct2D1.Brush brush = resCache["GridBrush"] as SharpDX.Direct2D1.Brush;

            int numYGrid = (int)((graphArea.Bottom - graphArea.Top) / this.YGridInterval);

            RawVector2 lineStart = new RawVector2(graphArea.Left, graphArea.Top);
            RawVector2 lineEnd = new RawVector2(graphArea.Right, graphArea.Top);
            for (int y = 0; y <= numYGrid; y++)
            {
                lineStart.Y = graphArea.Top + (graphArea.Bottom - graphArea.Top) * (float)y / (float)numYGrid;
                lineEnd.Y = lineStart.Y;
                target.DrawLine(lineStart, lineEnd, brush);

                String labelString = String.Format("{0:F2}", MaxY + (MinY - MaxY) * (double)y / (double)numYGrid);

                target.DrawText(labelString, YLabelFormat, OffsetLayoutBox(YLabelLayoutBox, lineStart.X, lineEnd.Y), brush);
            }
        }

        private void Plot(RenderTarget target)
        {
            RawRectangleF graphArea = new RawRectangleF(this.LeftMargin, this.TopMargin, (float)this.ActualWidth - this.RightMargin, (float)this.ActualHeight - this.BottomMargin);

            SharpDX.Direct2D1.Brush brush = resCache["PlotBrush"] as SharpDX.Direct2D1.Brush;

            lock (Values)
            {
                int count = Values.Length;
                if (count >= 2)
                {
                    RawVector2 start = new RawVector2();
                    RawVector2 end = new RawVector2()
                    {
                        X = graphArea.Left,
                        Y = graphArea.Bottom + (graphArea.Top - graphArea.Bottom) * (float)(Values[0] - MinY) / (float)(MaxY - MinY)
                    };
                    for (int i = 0; i < count - 1; i++)
                    {
                        start.X = end.X;
                        start.Y = end.Y;
                        end.X = graphArea.Left + (graphArea.Right - graphArea.Left) * (float)(i + 1) / (float)count;
                        end.Y = graphArea.Bottom + (graphArea.Top - graphArea.Bottom) * (float)(Values[i + 1] - MinY) / (float)(MaxY - MinY);

                        if (!float.IsNaN(start.Y) && !float.IsNaN(end.Y))
                            target.DrawLine(start, end, brush);
                    }
                }
            }
        }

        private void UpdateRanges()
        {
            MaxX = Values.Length;
            MaxY = Math.Max(Values.Max * 1.2, 10.0);
        }
        
        private void DrawStat(RenderTarget target)
        {
            if (StatMessage is String)
            {
                RawRectangleF statRect = new RawRectangleF()
                {
                    Top = (float)this.ActualHeight - 24,
                    Bottom = (float)this.ActualHeight,
                    Left = 0,
                    Right = (float)this.ActualWidth
                };

                target.DrawText(StatMessage, StatFormat, statRect, resCache["GridBrush"] as SharpDX.Direct2D1.Brush);
            }
        }

        public override void Render(RenderTarget target)
        {
            //target.AntialiasMode = AntialiasMode.Aliased;
            //target.TextAntialiasMode = SharpDX.Direct2D1.TextAntialiasMode.Aliased;

            target.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));

            UpdateRanges();
            DrawXGrid(target);
            DrawYGrid(target);
            Plot(target);
            DrawStat(target);
        }
    }

    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource CommunicationCanceller;
        private Thread Runner = null;

        private Double AccumulativeCurrentValue = 0;
        private UInt32 SampleCount = 0;

        private void UpdateStatistics()
        {
            String message;
            message = String.Format("累積電流量 {0:F2}[mAh]", this.AccumulativeCurrentValue * 3.6E-6);
            message += " ";
            message += String.Format("平均電流 {0:F2}[mA]", this.AccumulativeCurrentValue / (Double)SampleCount);
            this.GraphCanvas.StatMessage = message;
        }

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this;
        }

        private void StatisticsTimer_Tick(object sender, EventArgs e)
        {
            this.UpdateStatistics();
        }

        private void UpdateChart(UInt32 timestamp, double[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                this.GraphCanvas.Values.Push(samples[i], timestamp + (UInt32)i);
            }

            this.UpdateStatistics();
        }

        private delegate void UpdaterDelegate(UInt32 a, double[] b);

        private void CommunicationRunner(object _token)
        {
            using (WinUSBDevice device = new WinUSBDevice(new Guid("{50215a24-33bc-473e-83d9-b0215c461c7e}")))
            {
                CancellationToken token = (CancellationToken)_token;
                Byte[] buffer = new Byte[64];
                UInt32 bytesRead;

                device.ControlTransfer(0x41, 129, 0, 0, 0, null, 0);

                while (!token.IsCancellationRequested)
                {
                    bytesRead = device.ReadEndpoint(0x81, buffer, 64);

                    if (bytesRead >= 4 && bytesRead % 2 == 0)
                    {
                        BinaryReader reader = new BinaryReader(new MemoryStream(buffer));

                        UInt32 timestamp = reader.ReadUInt32();

                        double[] samples = new double[(bytesRead - 4) / 2];

                        for (int i = 0; i < samples.Length; i++)
                        {
                            Int16 val = reader.ReadInt16();
                            samples[i] = ((Double)val * 0.125);

                            this.AccumulativeCurrentValue += samples[i];
                            this.SampleCount++;
                        }
                        this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new UpdaterDelegate(this.UpdateChart), timestamp, samples);
                    }
                    Thread.Yield();
                }
            }
        }

        private void ConnectButton_Checked(object sender, RoutedEventArgs e)
        {
            this.CommunicationCanceller = new CancellationTokenSource();

            this.Runner = new Thread(new ParameterizedThreadStart(this.CommunicationRunner));

            this.Runner.Start(this.CommunicationCanceller.Token);

            ((ToggleButton)sender).Content = "切断";
        }

        private void ConnectButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.Runner is Thread && this.Runner.IsAlive)
            {
                this.CommunicationCanceller.Cancel();
                this.Runner.Join();
            }

            ((ToggleButton)sender).Content = "接続";
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (this.Runner is Thread && this.Runner.IsAlive)
            {
                this.CommunicationCanceller.Cancel();
                this.Runner.Join();
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            lock(this.GraphCanvas.Values)
            {
                this.GraphCanvas.Values.Flush();
                this.AccumulativeCurrentValue = 0;
                this.SampleCount = 0;
                this.UpdateStatistics();
            }
        }
    }
}
