using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;

namespace Stream
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DownstreamThread(null, null);
        }
        static int myPort = 5001;
        static UdpClient meUp = new UdpClient(myPort);
        static UdpClient meDown = new UdpClient(myPort + 1);
        static IPEndPoint peer = new IPEndPoint(IPAddress.Any, 0);
        static string peerIP = null;
        static int peerPort = 5003;
        private void Connect(object sender, RoutedEventArgs e)
        {
            peerIP = ip.Text;
            peerPort = Int32.Parse(port.Text);
            meDown.Connect(IPAddress.Parse(peerIP), peerPort);
            meUp.Connect(IPAddress.Parse(peerIP), peerPort + 1);
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
                    text.Text += dataText + "\n";
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("Attempting to reconnect in 2,5s");
                    Thread.Sleep(2500);
                }
            }
        }
        private void Host(object sender, RoutedEventArgs e)
        {
            try
            {
                byte[] data = meDown.Receive(ref peer);
                string dataText = Encoding.ASCII.GetString(data);
                text.Text += dataText + "\n";
                peerIP = peer.Address.ToString();
                peerPort = Int32.Parse(peer.Port.ToString());
                meDown.Connect(IPAddress.Parse(peerIP), peerPort);
                meUp.Connect(IPAddress.Parse(peerIP), peerPort + 1);
                string sendThis = "\nconnected";
                byte[] output = Encoding.ASCII.GetBytes(sendThis);
                meUp.Send(output, output.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void Upstream(object sender, RoutedEventArgs e)
        {
            try
            {
                string sendThis = message.Text;
                text.Text += message.Text + "\n";
                message.Text = "";
                byte[] output = Encoding.ASCII.GetBytes(sendThis);
                meUp.Send(output, output.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void Downstream(object sender, RoutedEventArgs e)
        {
            try
            {
                byte[] data = meDown.Receive(ref peer);
                string dataText = Encoding.ASCII.GetString(data);
                text.Text += dataText + "\n";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void DownstreamThread(object sender, RoutedEventArgs e)
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
                            text.Text += dataText + "\n";
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            });
        }
    }
}
