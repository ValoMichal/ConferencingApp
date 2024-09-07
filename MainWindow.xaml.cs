using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using AForge;
using AForge.Imaging;
using AForge.Imaging.Formats;
using AForge.Math;
using AForge.Video;
using AForge.Video.DirectShow;
using Accord;
using Accord.Video;
using System.Diagnostics;
using System.Collections;
using System.IO;
using System.Drawing.Imaging;
using System.IO.Ports;
using System.Globalization;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls;
using Accord.Collections;
using System.Runtime.CompilerServices;
using System.Printing.IndexedProperties;
using System.Windows.Interop;
using NAudio.Wave;
using System.Diagnostics.Tracing;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace Stream
{
    public partial class MainWindow : Window
    {
        private int camNum = 0;
        private int micNum = 0;
        private static bool camMute = true;
        private static bool micMute = true;
        private static WaveInEvent waveIn;
        private static WaveOutEvent waveOut;
        private static BufferedWaveProvider waveProvider;
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private static int chunkSize = 65000;
        private int id = 5004;
        private String name = new String("User");
        private static List<string> users = new List<string>();
        private static IPEndPoint peer = new IPEndPoint(IPAddress.Any, 0);
        UdpClient connect = new UdpClient(5000);
        UdpClient portChatUp = new UdpClient(5001);
        UdpClient portAudioUp = new UdpClient(5002);
        UdpClient portVideoUp = new UdpClient(5003);
        //UdpClient portChatDown = new UdpClient(5004);
        //UdpClient portAudioDown = new UdpClient(5005);
        //UdpClient portVideoDown = new UdpClient(5006);
        public MainWindow()
        {
            InitializeComponent();
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            try
            {
                InitializeCamera();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void SwitchCam(object sender, EventArgs e)
        {
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (camNum < videoDevices.Count - 1)
            {
                camNum++;
            }
            else
            {
                camNum = 0;
            }
            ((Button)sender).ToolTip = videoDevices[camNum].Name;
            CamOff(null, null);
            InitializeCamera();
        }
        private void SwitchMic(object sender, EventArgs e)
        {
            if (micNum < WaveInEvent.DeviceCount - 1)
            {
                micNum++;
            }
            else
            {
                micNum = 0;
            }
            ((Button)sender).ToolTip = WaveInEvent.GetCapabilities(micNum).ProductName;
            AudioUp(null, null);
        }
        private void MuteCam(object sender, EventArgs e)
        {
            camMute = !camMute;
            if (camMute)
            {
                Cam.Background = System.Windows.Media.Brushes.LightGray;
            }
            else
            {
                Cam.Background = System.Windows.Media.Brushes.Green;
            }
        }
        private void MuteMic(object sender, EventArgs e)
        {
            micMute = !micMute;
            if (micMute)
            {
                Mic.Background = System.Windows.Media.Brushes.LightGray;
            }
            else
            {
                Mic.Background = System.Windows.Media.Brushes.Green;
            }
        }
        private void InitializeCamera()
        {
            try
            {
                if (videoDevices.Count > 0)
                {
                    videoSource = new VideoCaptureDevice(videoDevices[camNum].MonikerString);
                    videoSource.NewFrame += VideoSource_NewFrame;
                    videoSource.Start();
                }
                else
                {
                    MessageBox.Show("No camera devices found.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Please check your camera and try again.");
            }
        }
        private void VideoSource_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            if (users.Count == 0)
            {
                if (camMute)
                {
                    Bitmap newFrame = new Bitmap(1920, 1080);
                    using (Graphics graph = Graphics.FromImage(newFrame))
                    {
                        Rectangle ImageSize = new Rectangle(0, 0, 1920, 1080);
                        graph.FillRectangle(System.Drawing.Brushes.Black, ImageSize);
                    }
                    this.Dispatcher.Invoke(() =>
                    {
                        Display.Source = Utils.BitmapToImageSource(newFrame);
                    });
                }
                else
                {
                    Bitmap newFrame = (Bitmap)eventArgs.Frame.Clone();
                    this.Dispatcher.Invoke(() =>
                    {
                        Display.Source = Utils.BitmapToImageSource(newFrame);
                    });
                }
            }
            else
            {
                VideoUp(sender, eventArgs);
                AudioUp(null,null);
            }
        }
        internal static class Utils
        {
            public static ImageSource BitmapToImageSource(Bitmap bitmap)
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                var memoryStream = new MemoryStream();

                bitmap.Save(memoryStream, ImageFormat.Bmp);
                memoryStream.Seek(0, SeekOrigin.Begin);
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }
        private void CamOff(object sender, EventArgs e)
        {
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.NewFrame -= VideoSource_NewFrame;
                //Dispatcher.InvokeShutdown();
                videoSource.SignalToStop();
                //videoSource.WaitForStop();
            }
        }
        private void Connect(object sender, RoutedEventArgs e)
        {
            name = Username.Text;
            Connection.Visibility = Visibility.Collapsed;
            Task.Run(() =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    users.Add(ip.Text);
                });
                //connect.Connect(users[users.Count-1], 5000);
                //while (true)
                //{
                    try
                    {
                        string sendThis = name;
                        byte[] output = Encoding.ASCII.GetBytes(sendThis);
                        connect.Send(output, output.Length, users[users.Count - 1], 5000);
                        connect.Client.ReceiveTimeout = 2500;
                        byte[] data = connect.Receive(ref peer);
                        string dataText = Encoding.ASCII.GetString(data);
                        id = Int32.Parse(dataText);
                        //peer.Port.ToString()
                        this.Dispatcher.Invoke(() =>
                        {
                            ChatAdd(sender, e, "Connected."); 
                        });
                    //ked sa konektnem nejdemi nic down 
                        //break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    //Debug.WriteLine("Attempting to reconnect in 2,5s");
                    //Thread.Sleep(2500);
                }
                Task.Run(() => VideoDown(null, null));
                Task.Run(() => AudioDown(null, null));
                Task.Run(() => ChatDown(null, null));
                //}
            });
        }
        private void Host(object sender, RoutedEventArgs e)
        {
            name = Username.Text;
            Connection.Visibility = Visibility.Collapsed;
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        byte[] data = connect.Receive(ref peer);
                        string dataText = Encoding.ASCII.GetString(data);
                        users.Add(peer.Address.ToString());
                        //connect.Connect(users[users.Count - 1], 5001 + users.Count * 3);
                        string sendThis = (5001 + users.Count * 3).ToString();
                        byte[] output = Encoding.ASCII.GetBytes(sendThis);
                        connect.Send(output, output.Length, users[users.Count - 1], 5000);
                        this.Dispatcher.Invoke(() =>
                        {
                            ChatAdd(sender, e, dataText + " connected.");
                        });
                        Task.Run(() => VideoDown(null, null));
                        Task.Run(() => AudioDown(null, null));
                        Task.Run(() => ChatDown(null, null));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            });
        }
        private void ChatUpEnter(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter|| e.Key == System.Windows.Input.Key.Return)
            {
                ChatUp(null, null);
                e.Handled = true;
            }
        }
        private void ConnectEnter(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Return)
            {
                Connect(null, null);
                e.Handled = true;
            }
        }
        private void ChatAdd(object sender, RoutedEventArgs e, String msg)
        {
            TextBlock newTextBlock = new TextBlock();
            newTextBlock.Text = msg;
            newTextBlock.TextWrapping = TextWrapping.Wrap;
            Chat.Children.Insert(Chat.Children.Count, newTextBlock);
            if (Chat.Children.Count > 25)
            {
                Chat.Children.RemoveAt(0);
            }
            scrollViewer.ScrollToEnd();
        }
        private void ChatUp(object sender, RoutedEventArgs e)
        {
            try
            {
                string sendThis = name+": "+Msg.Text;
                ChatAdd(sender, e, sendThis);
                Msg.Text = "";
                byte[] output = Encoding.ASCII.GetBytes(sendThis);
                for (int i = 0; i<users.Count; i++)
                {
                    portChatUp.Send(output, output.Length, users[i], id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void AudioUp(object sender, RoutedEventArgs e)
        {
            if (waveIn != null)
            {
                waveIn.Dispose();
            }
            waveIn = new WaveInEvent
            {
                DeviceNumber = micNum,
                WaveFormat = new WaveFormat(44100, 16, 1)
            };
            waveIn.DataAvailable += (sender, e) => WaveIn_DataAvailable(sender, e, portAudioUp);
            waveIn.StartRecording();

        }
        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e, UdpClient port)
        {
            if (!micMute)
            {
                for (int i = 0; i < users.Count; i++)
                {
                    port.Send(e.Buffer, e.BytesRecorded, users[i], id+1);
                }
            }
        }
        private void VideoUp(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                if (camMute)
                {
                    Bitmap bmp = new Bitmap(1920, 1080);
                    using (Graphics graph = Graphics.FromImage(bmp))
                    {
                        Rectangle ImageSize = new Rectangle(0, 0, 1920, 1080);
                        graph.FillRectangle(System.Drawing.Brushes.Black, ImageSize);
                    }
                    bmp.Save(ms, ImageFormat.Jpeg);
                }
                else
                {
                    eventArgs.Frame.Save(ms, ImageFormat.Jpeg);
                }
                byte[] buffer = ms.ToArray();
                int totalChunks = (buffer.Length + chunkSize - 1) / chunkSize;
                for (int i = 0; i < totalChunks; i++)
                {
                    int currentChunkSize = Math.Min(chunkSize, buffer.Length - i * chunkSize);
                    byte[] chunk = new byte[currentChunkSize + 4];
                    BitConverter.GetBytes(i).CopyTo(chunk, 0);
                    Array.Copy(buffer, i * chunkSize, chunk, 4, currentChunkSize);
                    try
                    {
                        for (int j = 0; j < users.Count; j++)
                        {
                            portVideoUp.Send(chunk, chunk.Length, users[j], id+2);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }
        }
        private void ChatDown(object sender, RoutedEventArgs e)
        {
            UdpClient port = new UdpClient(5001 + 3 * users.Count);
            while (true)
            {
                try
                {
                    byte[] data = port.Receive(ref peer);
                    string dataText = Encoding.ASCII.GetString(data);
                    this.Dispatcher.Invoke(() =>
                    {
                        ChatAdd(sender, e, dataText);
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
        private void AudioDown(object sender, RoutedEventArgs e)
        {
            UdpClient port = new UdpClient(5002+3*users.Count);
            waveProvider = new BufferedWaveProvider(new WaveFormat(44100, 16, 1));
            waveOut = new WaveOutEvent();
            waveOut.Init(waveProvider);
            waveOut.Play();
            StartReceiving(port);
        }
        private static void StartReceiving(UdpClient port)
        {
            port.BeginReceive((ar) => OnDataReceived(ar, port), null);
        }
        private static void OnDataReceived(IAsyncResult ar, UdpClient port)
        {
            byte[] receivedBytes = port.EndReceive(ar, ref peer);
            waveProvider.AddSamples(receivedBytes, 0, receivedBytes.Length);
            StartReceiving(port);
        }
        private void VideoDown(object sender, RoutedEventArgs e)
        {
            UdpClient port = new UdpClient(5003 + 3 * users.Count);
            Dictionary<int, byte[]> receivedChunks = new Dictionary<int, byte[]>();
            try
            {
                byte[] data = port.Receive(ref peer);
                int chunkIndex = BitConverter.ToInt32(data, 0);
                byte[] chunkData = new byte[data.Length - 4];
                Array.Copy(data, 4, chunkData, 0, chunkData.Length);
                receivedChunks[chunkIndex] = chunkData;
                Debug.WriteLine($"received chunk {chunkIndex}");
                while (true)
                {
                    data = port.Receive(ref peer);
                    chunkIndex = BitConverter.ToInt32(data, 0);
                    if (chunkIndex == 0)
                    {
                        int pictureSize = 0;
                        int lengthSum = 0;
                        foreach (var chunk in receivedChunks.OrderBy(kv => kv.Key))
                        {
                            pictureSize += chunk.Value.Length;
                        }
                        byte[] picture = new byte[receivedChunks.Count * chunkSize];
                        foreach (var chunk in receivedChunks.OrderBy(kv => kv.Key))
                        {
                            Array.Copy(chunk.Value, 0, picture, lengthSum, chunk.Value.Length);
                            lengthSum += chunk.Value.Length;
                        }
                        Dispatcher.Invoke(() =>
                        {
                            BitmapImage bitmapImage = ConvertToBitmapImage(picture);
                            Display.Source = bitmapImage;
                        });
                    }
                    chunkData = new byte[data.Length - 4];
                    Array.Copy(data, 4, chunkData, 0, chunkData.Length);
                    receivedChunks[chunkIndex] = chunkData;
                    Debug.WriteLine($"received chunk {chunkIndex}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
        private BitmapImage ConvertToBitmapImage(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = ms;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }
    }
}
//todo
//host shares not himself to everyone but the loudest users video, all audio and all chat
