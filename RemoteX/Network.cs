using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace RemoteX
{
    public partial class MainWindow
    {
        public Boolean G_threadrunning = false;
        public string G_pcname = "", G_pcIP = "", G_remotename = "", G_remoteip = "";

        public Boolean G_disconnect = false;
        static TcpListener G_listener;
        public NetworkStream G_stream;
        public StreamReader G_streamreader;
        public StreamWriter G_streamwriter;
        Socket G_socket = null;
        Thread G_Listener_thread;
        Thread G_sendfile_thread;

        private void start_thread()
        {
            try
            {
                G_Listener_thread = new Thread(new ThreadStart(acceptingsockets));
                G_Listener_thread.Start();
            }
            catch (Exception f)
            {
                Debug.WriteLine(f.Message);
            }
        }

        private void acceptingsockets()
        {
            try
            {                
                Debug.WriteLine("Listening......");
                G_socket = G_listener.AcceptSocket();                
            }
            catch (Exception f)
            {
                Debug.WriteLine(f.Message);
            }

            try
            {
                G_stream = new NetworkStream(G_socket);
                G_streamreader = new StreamReader(G_stream);
                G_streamwriter = new StreamWriter(G_stream);
                G_streamwriter.AutoFlush = true;
                G_streamwriter.Flush();
                string networkmessage = "";

                networkmessage = crypt_ReadLine();
                Debug.WriteLine("what " + networkmessage);
                if (networkmessage != "connected")
                {
                    throw new System.ArgumentException("connection error", "not connected");
                }
                else
                {
                    crypt_WriteLine("connected");
                    Start_Recieving();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("every thing ends: " + e.Message);
            }

            try
            {
                G_disconnect = true;
                disconnect_network();

            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }

        }

        public void Start_Recieving()
        {

            String networkmessage = "";
            while (true) // keep listening for messages until disconnects
            {
                
                if (G_disconnect)
                {
                    disconnect_network();
                    break;
                }
                Debug.WriteLine("Waiting for msg....");

                networkmessage = crypt_ReadLine();

                Debug.WriteLine(networkmessage);
                if (networkmessage != null && networkmessage.Length > 0)
                {
                    if (networkmessage.Equals("syncback"))
                    {
                        if (G_sendfile_thread != null && G_sendfile_thread.IsAlive)
                        {
                            Debug.WriteLine("closed file thread");
                            G_sendfile_thread.Abort();
                            G_sendfile_thread = null;
                        }
                    }
                    else if (networkmessage[0] == '!')
                    {
                        screen_mouse(networkmessage.Substring(1, networkmessage.Length - 1) + ";#");  //screensharing
                        sendscreen();
                    }
                    else if (networkmessage[0] == '*')
                    {
                        movemouse(networkmessage.Substring(1, networkmessage.Length - 1) + "#");   //mouse 
                    }
                    else if (networkmessage[0] == '@')
                    {
                        extractkey(networkmessage.Substring(1, networkmessage.Length - 1) + "#");    //keyboard
                    }
                    else if (networkmessage[0] == '%')
                    {
                        shortcut(networkmessage.Substring(1, networkmessage.Length - 1));        // shortcutkeys
                    }
                    else if (networkmessage[0] == '$')
                    {
                        sendsysinfo(networkmessage.Substring(1, networkmessage.Length - 1));    //system info
                    }
                    else if (networkmessage[0] == '&')
                    {
                        explorer_actions(networkmessage.Substring(1, networkmessage.Length - 1));   //explorer
                    }
                    else if (networkmessage[0] == '^')
                    {
                        specialaction(networkmessage.Substring(1, networkmessage.Length - 1)); //special actions
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    Debug.WriteLine("No  message found");
                    break;
                }

            }
            //something wrongoccured
            Debug.WriteLine("while loop ends please");
            crypt_WriteLine("Disconnect");

        }



        public static IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }
            return new IPAddress(broadcastAddress);
        }



        private async void send_udp_broadcast()
        {
            UdpClient client = new UdpClient();
            UnicastIPAddressInformation uni_info = getlocalip();
            IPAddress broad_ip = GetBroadcastAddress(uni_info.Address, uni_info.IPv4Mask );
            IPEndPoint ip = new IPEndPoint(broad_ip, 2601);
            while (true)
            {
                byte[] bytes = Encoding.ASCII.GetBytes("self_broadcast");
                client.Send(bytes, bytes.Length, ip);
                Debug.WriteLine("koi mai ka laal " + broad_ip.ToString());
                await Task.Delay(2000);
            }
            client.Close();
        }
        


        private async void receive_udp_broadcast()
        {

            Debug.WriteLine("Start recieivng");
            int PORT = 2600;
            UdpClient udpClient = new UdpClient();
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, PORT));

            var from = new IPEndPoint(0, 0);
            Task.Run(() =>
            {
                while (true)
                {
                    var recvBuffer = udpClient.Receive(ref from);
                    Debug.WriteLine("recieved " + Encoding.UTF8.GetString(recvBuffer));
                }
            });

        }
       





        private async void check_isalive()
        {
            Debug.WriteLine("check is alive starts loop");


            int udp_port = 2600;
            UdpClient udp_reciever = new UdpClient(udp_port);
            udp_reciever.Client.ReceiveTimeout = 1000;
            IPEndPoint IPendpoint = new IPEndPoint(IPAddress.Any, udp_port);
            int recieved_count = 0;

            while (true)
            {
                Debug.WriteLine("check is alive");
                if (G_socket!=null && G_socket.Connected)
                {
                    try
                    {
                        String msg = "";
                        byte[] msg_bytes = udp_reciever.Receive(ref IPendpoint);
                        Debug.WriteLine(IPendpoint.ToString());
                        send_udp_broadcast();
                        msg = Encoding.ASCII.GetString(msg_bytes, 0, msg_bytes.Length);
                        Debug.WriteLine(msg);
                        if(msg.Contains("IamAlive"))
                        {
                            recieved_count = 0;
                        }
                        else if (msg.Contains("self_broadcast"))
                        {
                            // do nothing
                        }
                        else
                        {
                            throw new System.InvalidOperationException("No message Recieved");
                        }   

                        Debug.WriteLine(msg + " " + msg.Length);
                    }
                    catch (Exception e)
                    {
                        recieved_count -= 1;
                        Debug.WriteLine("udprecieve error " + e.Message);
                    }

                    if (recieved_count <-1)
                    {
                        //disconnect_network();
                        recieved_count = 0;
                    }

                }
                await Task.Delay(3000);
            }
            Debug.WriteLine("check is alive ends loop");
        }

        public void disconnect_network()
        {

            try
            {
                G_streamwriter.Close();
                G_streamwriter = null;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            try
            {
                G_streamreader.Close();
                G_streamreader = null;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            try
            {
                G_stream.Close();
                G_stream = null;

            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
            
            try
            {
                G_socket.Shutdown(SocketShutdown.Both);
                G_socket.Close();
                G_socket = null;


            }
            catch (Exception e)
            {
                Debug.WriteLine("socket shutdown " + e);
            }

            try
            {
                G_Listener_thread.Abort();

            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            G_threadrunning = false;
            G_disconnect = false;
            Debug.WriteLine("Disconneted");
        }





        //get local ip address
        public UnicastIPAddressInformation getlocalip()
        {
            IPAddress[] host;
            UnicastIPAddressInformation localIP = null;
            host = Dns.GetHostAddresses(Dns.GetHostName());

            //if(G_socket!=null)
            //Debug.WriteLine(G_socket.LocalEndPoint.ToString());

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                //Debug.WriteLine(nic.Name);
                foreach (UnicastIPAddressInformation IP_info in nic.GetIPProperties().UnicastAddresses)
                {
                    if (IP_info.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        //Debug.WriteLine(ip.Address.ToString());
                        if (!IPAddress.IsLoopback(IP_info.Address) && host.Contains(IP_info.Address)
                            && !nic.Name.ToLower().Contains("loopback") )                            
                        {
                            localIP = IP_info;
                        }
                    }
                }
            }

            return localIP;
        }
    }
}
