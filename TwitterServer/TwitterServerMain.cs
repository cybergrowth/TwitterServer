using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace TwitterServer
{
    internal class TwitterServerMain
    {
        //server socket data
        private static readonly string serverIp = File.ReadAllText(Environment.CurrentDirectory+"\\ipport.txt").Split(':')[0];
        private static readonly int port = Int32.Parse(File.ReadAllText(Environment.CurrentDirectory + "\\ipport.txt").Split(':')[1]);

        //database connection string
        private const string connectString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=|DataDirectory|\\Users.mdf;Integrated Security=True";
        
        //list of all connected users by username
        public static List<string> connectedUsers=new List<string>();
        
        //list of all blocked ip addresses
        internal static List<string> blockedIps=new List<string>();

        // used to block Brute force attack and ddos attacks
        internal static Dictionary<string, int> IPSCORE = new Dictionary<string, int>();

        // time between two connection attemts
        private static Dictionary<string, DateTime> lastConnection = new Dictionary<string, DateTime>();
        
        //point where an IP score bans an ip
        internal const int banPoint = 100;
        //allowed time before different connections without a strike in seconds
        private const double allowedMinConnectionDifference = 2.5;

        /// <summary>
        /// main function which is responcible for reciving new sockets and handing them over to the login class
        /// it is also responcible for blocking blacklisted users
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            //this is the server
            TwitsManager.Setup(connectString);
            TwitSearcher.Setup(connectString);
            UsersSqlHandler.Setup(connectString);
            UserProfilesManager.Setup();
            TwitFileManager.Setup();

            TcpListener tcpListener = new TcpListener(IPAddress.Parse(serverIp), port);
            try
            {
                tcpListener.Start();
            }
            catch {
                //invalid addres
                Logging.Log("Invalid address");
                Thread.Sleep(1);
                Environment.Exit(0);
            }
            Logging.Log("Server started on "+serverIp+" on port "+port);


            string ip;
            while (true) {
                TcpClient client = tcpListener.AcceptTcpClient();

                //blocked users handling
                ip = client.Client.RemoteEndPoint.ToString().Split(':')[0];
                if (blockedIps.Contains(ip)) {
                    Logging.Log("blocked "+ip);
                    continue;
                }

                if (lastConnection.Keys.Contains(ip))
                {
                    if (allowedMinConnectionDifference > DateTime.Now.Subtract(lastConnection[ip]).TotalSeconds) {
                        IPSCORE[ip] += 1;
                    }
                    if (IPSCORE[ip] >= banPoint) {
                        blockedIps.Add(ip);
                        continue;
                    }

                    lastConnection[ip]= DateTime.Now;

                }
                else {
                    lastConnection.Add(ip, DateTime.Now);
                    IPSCORE.Add(ip, 0);

                }

                //checking if it is login,register,or reset password
                new Thread(() =>
                {
                    UserCommunication user = new UserCommunication(client);
                    string output = Encoding.UTF8.GetString(user.ReadBytes());

                    if (output[0] == '0')
                    {
                        LoginUtils.login(user, output);
                    }

                    else if (output[0] == '1')
                    {
                        LoginUtils.register(user, output);
                    }
                    else {
                        JsonElement root=JsonDocument.Parse(output.Substring(1)).RootElement;
                        LoginUtils.ResetPassword(user,root.GetProperty("username").ToString(), root.GetProperty("email").ToString(), root.GetProperty("password").ToString());
                    }
                }).Start(); 
            }
            
        }    
    }
}
