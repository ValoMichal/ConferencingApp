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

namespace streamClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }
        static int port = 5001;
        static UdpClient me = new UdpClient(port);
        static IPEndPoint server = new IPEndPoint(IPAddress.Any, 0);
        static string serverIP = null;
        static int serverPort = 5002;
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Connect();
                Downstream();
                Upstream();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void Connect()
        {
            serverIP = "10.0.2.15";
            while (true)
            {
                try
                {
                    me.Connect(IPAddress.Parse(serverIP), serverPort);
                    text.Text += "Connected";
                    string sendThis = "This is test";
                    byte[] output = Encoding.ASCII.GetBytes(sendThis);
                    me.Send(output, output.Length);
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("Attempting to reconnect in 2,5s");
                }
                Thread.Sleep(2500);
            }
        }
        private void Host()
        {
            try
            {
                me.Receive(ref server);
                serverIP = server.Address.ToString();
                serverPort = Int32.Parse(server.Port.ToString())+1;
                me.Connect(IPAddress.Parse(serverIP), serverPort);
                text.Text += "Connected";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void Upstream()
        {
            try
            {
                string sendThis = "This is test";
                byte[] output = Encoding.ASCII.GetBytes(sendThis);
                me.Send(output, output.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void Downstream()
        {
            try
            {
                byte[] data = me.Receive(ref server);
                string message = Encoding.ASCII.GetString(data);
                text.Text = message;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
