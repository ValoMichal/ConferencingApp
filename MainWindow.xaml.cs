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
using NAudio.Wave.SampleProviders;
using Concentus;
using Concentus.Structs;
using Concentus.Enums;

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
        private static OpusEncoder opusEncoder;
        private static OpusDecoder opusDecoder;
        private List<float> volumes = new List<float>();
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private static int chunkSize = 65000;
        private int id = 1;
        private String name = new String("User");
        private static List<string> users = new List<string>();
        private static IPEndPoint peer = new IPEndPoint(IPAddress.Any, 0);
        UdpClient connect = new UdpClient(5000);
        UdpClient portChatUp = new UdpClient(5001);
        UdpClient portAudioUp = new UdpClient(5002);
        UdpClient portVideoUp = new UdpClient(5003);
        private String myIP = new String("");
        //UdpClient portChatDown = new UdpClient(5004);
        //UdpClient portAudioDown = new UdpClient(5005);
        //UdpClient portVideoDown = new UdpClient(5006);
        public MainWindow()
        {
            for (int i = 0; i < 100; i++)
            {
                volumes.Add(0);
            }
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
            //
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
                AudioUp(null, null);
            }
            //
            /*
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
            if (users.Count != 0)
            {
                VideoUp(null, null);
                AudioUp(null, null);
            }
            */
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
                    string sendThis = string.Join(Environment.NewLine, users);
                    byte[] output = Encoding.ASCII.GetBytes(sendThis);
                    connect.Send(output, output.Length, users[0], 5000);
                    //connect.Client.ReceiveTimeout = 2500;
                    byte[] data = connect.Receive(ref peer);
                    string dataText = Encoding.ASCII.GetString(data);
                    List<string> receivedList = new List<string>(dataText.Split(new[] { Environment.NewLine }, StringSplitOptions.None));
                    id = receivedList.Count;
                    if (users.Count < 2)
                    {
                        users[0] = receivedList[receivedList.Count - 1];
                    }
                    for (int i = 0; i < receivedList.Count; i++)
                    {
                        for (int j = 0; j < users.Count; j++)
                        {
                            if (receivedList[i].Equals(users[j]))
                            {
                                i++;
                                j = 0;
                            }
                        }
                        users.Add(receivedList[i]);
                    }
                    //id = Int32.Parse(dataText);
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
            //Connection.Visibility = Visibility.Collapsed;
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        byte[] data = connect.Receive(ref peer);
                        string dataText = Encoding.ASCII.GetString(data);
                        List<string> receivedList = new List<string>(dataText.Split(new[] { Environment.NewLine }, StringSplitOptions.None));
                        for (int i = 0; i < receivedList.Count; i++)
                        {
                            for (int j = 0; j < users.Count; j++)
                            {
                                if (receivedList[i].Equals(users[j]))
                                {
                                    i++;
                                    j = 0;
                                }
                            }
                            users.Add(receivedList[i]);
                        }
                        users.Add(peer.Address.ToString());
                        //connect.Connect(users[users.Count - 1], 5001 + users.Count * 3);
                        string sendThis = string.Join(Environment.NewLine, users);
                        byte[] output = Encoding.ASCII.GetBytes(sendThis);
                        for (int i = 1; i < users.Count; i++)
                        {
                            connect.Send(output, output.Length, users[i], 5000);
                        }
                        Debug.WriteLine(users.Count.ToString());
                        this.Dispatcher.Invoke(() =>
                        {
                            ChatAdd(sender, e, "connected.");
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
            if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Return)
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
                string sendThis = name + ": " + Msg.Text;
                ChatAdd(sender, e, sendThis);
                Msg.Text = "";
                byte[] output = Encoding.ASCII.GetBytes(sendThis);
                for (int i = 1; i < users.Count; i++)
                {
                    portChatUp.Send(output, output.Length, users[i], 5001 + 3 * i);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void AudioUp(object sender, RoutedEventArgs e)
        {
            opusEncoder = new OpusEncoder(48000, 1, OpusApplication.OPUS_APPLICATION_AUDIO);
            if (waveIn != null)
            {
                waveIn.Dispose();
            }
            waveIn = new WaveInEvent
            {
                DeviceNumber = micNum,
                WaveFormat = new WaveFormat(48000, 16, 1),
                BufferMilliseconds = 20
            };
            waveIn.DataAvailable += (sender, e) => WaveIn_DataAvailable(sender, e, portAudioUp);
            waveIn.StartRecording();

        }
        private async void WaveIn_DataAvailable(object sender, WaveInEventArgs e, UdpClient port)
        {
            byte[] opusEncoded = EncodeAudioToOpus(e.Buffer, e.BytesRecorded);
            var tasks = new List<Task>();
            if (!micMute && opusEncoded != null)
            {
                for (int i = 1; i < users.Count; i++)
                {
                    tasks.Add(port.SendAsync(opusEncoded, opusEncoded.Length, users[i], 5002 + 3 * i));
                    //port.Send(e.Buffer, e.BytesRecorded, users[i], 5002 + 3 * i);
                }
                await Task.WhenAll(tasks);
            }
        }
        private static byte[] EncodeAudioToOpus(byte[] pcmData, int bytesRecorded)
        {
            // Convert PCM bytes to short samples (assuming 16-bit PCM format)
            short[] pcmSamples = new short[bytesRecorded / 2]; // 2 bytes per sample
            Buffer.BlockCopy(pcmData, 0, pcmSamples, 0, bytesRecorded);

            // Create a buffer to hold the Opus-encoded data
            byte[] opusEncoded = new byte[pcmSamples.Length]; // Typically smaller than PCM size
            int encodedBytes = 0;
            if (pcmSamples.Length != 0)
            {
                try
                {
                    encodedBytes = opusEncoder.Encode(pcmSamples, 0, pcmSamples.Length, opusEncoded, 0, opusEncoded.Length);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            }

            if (encodedBytes > 0)
            {
                // Return only the encoded bytes
                return opusEncoded.Take(encodedBytes).ToArray();
            }
            else
            {
                Console.WriteLine("Failed to encode audio.");
                return null;
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
                            portVideoUp.Send(chunk, chunk.Length, users[j], 5003 + 3 * j);
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
            int thisID = users.Count;
            UdpClient port = new UdpClient(5001 + 3 * (thisID - 1));
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
            opusDecoder = new OpusDecoder(48000, 1);
            int thisID = users.Count;
            volumes[thisID] = 0;
            UdpClient port = new UdpClient(5002 + 3 * (thisID - 1));
            waveProvider = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
            {
                BufferDuration = TimeSpan.FromSeconds(5)
            };
            waveOut = new WaveOutEvent();
            waveOut.Init(waveProvider);
            waveOut.Play();
            StartReceiving(port);
            while (true)
            {
                volumes[thisID] = CalculateRMS(waveProvider);
                Thread.Sleep(1000);
            }
        }

        private static void StartReceiving(UdpClient port)
        {
            while (true)
            {
                // Receive the encoded Opus data
                byte[] receivedData = port.Receive(ref peer);

                // Decode the Opus data back to PCM
                short[] pcmData = DecodeOpusToPCM(receivedData);
                if (pcmData != null)
                {
                    // Convert PCM data (short[]) back to byte[] for playback
                    byte[] pcmBytes = new byte[pcmData.Length * 2]; // 2 bytes per sample
                    Buffer.BlockCopy(pcmData, 0, pcmBytes, 0, pcmBytes.Length);

                    // Add PCM data to NAudio's buffer for playback
                    waveProvider.AddSamples(pcmBytes, 0, pcmBytes.Length);
                }
            }
        }
        /*
        private static void StartReceiving(UdpClient port)
        {
            port.BeginReceive((ar) => OnDataReceived(ar, port), null);
        }
        private static void OnDataReceived(IAsyncResult ar, UdpClient port)
        {
            byte[] receivedBytes = port.EndReceive(ar, ref peer);
            waveProvider.AddSamples(receivedBytes, 0, receivedBytes.Length);
            receivedBytes = null;
            StartReceiving(port);
        }
        */
        private static float CalculateRMS(BufferedWaveProvider waveProvider)
        {
            byte[] buffer = new byte[waveProvider.BufferLength];
            int bytesRead = waveProvider.Read(buffer, 0, buffer.Length);

            if (bytesRead == 0)
                return 0.0f;

            // Convert byte array to float array (assuming 16-bit PCM)
            int sampleCount = bytesRead / 2;
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(buffer, i * 2);
                samples[i] = sample / 32768f; // Normalize to [-1.0, 1.0]
            }

            // Calculate RMS (Root Mean Square)
            float sumSquares = samples.Select(sample => sample * sample).Sum();
            return (float)Math.Sqrt(sumSquares / sampleCount);
        }

        private static short[] DecodeOpusToPCM(byte[] encodedData)
        {
            // Create a buffer to hold the decoded PCM data
            short[] pcmSamples = new short[48000]; // Allocate enough space for 1 second of audio (48k samples for mono)

            // Decode Opus data into PCM format
            int decodedSamples = opusDecoder.Decode(encodedData, 0, encodedData.Length, pcmSamples, 0, pcmSamples.Length, false);

            if (decodedSamples > 0)
            {
                // Return only the decoded samples
                return pcmSamples[..decodedSamples];
            }
            else
            {
                Console.WriteLine("Failed to decode Opus audio.");
                return null;
            }
        }
        private void VideoDown(object sender, RoutedEventArgs e)
        {
            /*
            int objectNumber = 0;
            this.Dispatcher.Invoke(() =>
            {
                objectNumber = grid.Children.Count;
            });
            */
            int thisID = users.Count;
            UdpClient port = new UdpClient(5003 + 3 * (thisID - 1));
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
                            /*
                            System.Windows.Controls.Image newImage = new System.Windows.Controls.Image();
                            objectNumber = grid.Children.Count;
                            newImage.Stretch = Stretch.Uniform;
                            newImage.Source = bitmapImage;
                            grid.Children.Insert(objectNumber, newImage);
                            */
                            if (volumes.Max() == volumes[thisID])
                            {
                                Display.Source = bitmapImage;
                            }
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
