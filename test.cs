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
using System.Runtime.CompilerServices;

namespace streamServer
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
        static UdpClient me = new UdpClient(5002);
        static IPEndPoint client = new IPEndPoint(IPAddress.Any, 0);
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                byte[] data = me.Receive(ref client);
                string message = Encoding.ASCII.GetString(data);
                text.Text = message;
                me.Connect(IPAddress.Parse(client.Address.ToString()),5001);
                text.Text += "Connected";
                string sendThis = "This is test message";
                byte[] output = Encoding.ASCII.GetBytes(sendThis);
                me.Send(output, output.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }finally
            {
                me.Close();
            }
        }
    }
}
