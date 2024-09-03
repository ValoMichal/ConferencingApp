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

namespace Stream
{
    public partial class MainWindow : Window
    {
        //
        static int chunkSize = 65000;
        //
        static int myPort = 5000;
        static UdpClient meUp = new UdpClient(myPort + 1);
        static UdpClient meDown = new UdpClient(myPort);
        static IPEndPoint peer = new IPEndPoint(IPAddress.Any, 0);
        static string peerIP = null;
        static int peerPort = myPort;
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private int camNum = 0;
        private int micNum = 0;
        private bool camMute = true;
        private bool micMute = true;
        private int id = 0;
        private List<string> users = new List<string>();
        public MainWindow()
        {
            InitializeComponent();
            //ChatDown(null, null);
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
            CamOff(null, null);
            InitializeCamera();
        }
        private void SwitchMic(object sender, EventArgs e)
        {
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
            if (id == 0 && users.Count == 0)
            {
                using (Bitmap newFrame = (Bitmap)eventArgs.Frame.Clone())
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        Display.Source = Utils.BitmapToImageSource(newFrame);
                    });
                }
            }
            else
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    eventArgs.Frame.Save(ms, ImageFormat.Jpeg);
                    byte[] buffer = ms.ToArray();
                    //
                    int totalChunks = (buffer.Length + chunkSize - 1) / chunkSize;
                    for (int i = 0; i < totalChunks; i++)
                    {
                        int currentChunkSize = Math.Min(chunkSize, buffer.Length - i * chunkSize);
                        byte[] chunk = new byte[currentChunkSize + 4];
                        BitConverter.GetBytes(i).CopyTo(chunk, 0);
                        Array.Copy(buffer, i * chunkSize, chunk, 4, currentChunkSize);

                        //
                        try
                        {
                            meUp.Send(chunk, chunk.Length);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                        //
                    }
                    //
                }
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
            Task.Run(() =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    peerIP = ip.Text;
                });
                peerPort = myPort;
                meDown.Connect(IPAddress.Parse(peerIP), peerPort + 1);
                meUp.Connect(IPAddress.Parse(peerIP), peerPort);
                while (true)
                {
                    try
                    {
                        string sendThis = "\nconnected";
                        byte[] output = Encoding.ASCII.GetBytes(sendThis);
                        meUp.Send(output, output.Length);
                        meDown.Client.ReceiveTimeout = 2500;
                        byte[] data = meDown.Receive(ref peer);
                        string dataText = Encoding.ASCII.GetString(data);
                        this.Dispatcher.Invoke(() =>
                        {
                            Chat.Text += dataText + "\n";
                        });
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        Debug.WriteLine("Attempting to reconnect in 2,5s");
                        Thread.Sleep(2500);
                    }
                }
                id = 1;
                VideoDown(null, null);
            });
        }
        private void Host(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    byte[] data = meDown.Receive(ref peer);
                    string dataText = Encoding.ASCII.GetString(data);
                    this.Dispatcher.Invoke(() =>
                    {
                        Chat.Text += dataText + "\n";
                    });
                    peerIP = peer.Address.ToString();
                    //peerPort = Int32.Parse(peer.Port.ToString());
                    meDown.Connect(IPAddress.Parse(peerIP), peerPort + 1);
                    meUp.Connect(IPAddress.Parse(peerIP), peerPort);
                    string sendThis = "\nconnected";
                    byte[] output = Encoding.ASCII.GetBytes(sendThis);
                    meUp.Send(output, output.Length);
                    users.Add(peerIP);
                    VideoDown(null, null);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            });
        }
        private void ChatUp(object sender, RoutedEventArgs e)
        {
            try
            {
                string sendThis = Msg.Text;
                Chat.Text += Msg.Text + "\n";
                Msg.Text = "";
                byte[] output = Encoding.ASCII.GetBytes(sendThis);
                meUp.Send(output, output.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void ChatDown(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        byte[] data = meDown.Receive(ref peer);
                        string dataText = Encoding.ASCII.GetString(data);
                        this.Dispatcher.Invoke(() =>
                        {
                            Chat.Text += dataText + "\n";
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            });
        }
        private void VideoUp(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        MemoryStream stream = new MemoryStream();
                        //bitmap.Save(stream, ImageFormat.Jpeg);
                        byte[] output = stream.ToArray();
                        meUp.Send(output, output.Length);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            });
        }
        private void VideoDown(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                //
                Dictionary<int, byte[]> receivedChunks = new Dictionary<int, byte[]>();
                try
                {
                    byte[] data = meDown.Receive(ref peer);
                    int chunkIndex = BitConverter.ToInt32(data, 0);
                    byte[] chunkData = new byte[data.Length - 4];
                    Array.Copy(data, 4, chunkData, 0, chunkData.Length);
                    receivedChunks[chunkIndex] = chunkData;
                    Debug.WriteLine($"receoved chunk {chunkIndex}");
                    //
                    while (true)
                    {
                        data = meDown.Receive(ref peer);
                        //
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
                            //
                            Dispatcher.Invoke(() =>
                            {
                                BitmapImage bitmapImage = ConvertToBitmapImage(picture);//chunkData instead data
                                Display.Source = bitmapImage;
                            });
                            //
                        }
                        chunkData = new byte[data.Length - 4];
                        Array.Copy(data, 4, chunkData, 0, chunkData.Length);
                        receivedChunks[chunkIndex] = chunkData;
                        Debug.WriteLine($"receoved chunk {chunkIndex}");
                        //
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            });
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
//upstream will have foreach ip send video
//show only loudest user or cycle through manually
//i would like chat to go from bottom and oldest messages dissapear at top but thats not priority as chat wont be used for such long convos
//connect by texting to port 5000 and receive your call id to determine port u gonna use
//finish sending video, then start doing multiuser (prop just put everything in arrays), the clean up code
//rewrite it so sockets are created inside threads. there will be host thread that will be creating upstream and downstream threads
//when someone connects you save his ip and give him id
//server shares the loudest user video and all audio and chat?
