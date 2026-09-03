using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace TwitterServer
{
    /// <summary>
    /// handles all comms between client and server
    /// </summary>
    internal class UserCommunication
    {
        //encryption
        public const int aesKeySize = 32; 
        TcpClient tcpClient;
        RSA rSA;
        Aes aes;
        //connection
        DateTime lastUpdate;
        bool isNotClosed;
        //user data
        string username;
        private string ip;


        /// <summary>
        /// setup encrypted communication
        /// </summary>
        public UserCommunication(TcpClient tcpClient)
        {
            ip = tcpClient.Client.RemoteEndPoint.ToString();

            //RSA public key exchange
            this.tcpClient = tcpClient;
            byte[] data = new byte[tcpClient.ReceiveBufferSize];
            tcpClient.GetStream().Read(data, 0, data.Length);
            string rsaString = Encoding.UTF8.GetString(data);
            rSA = RSA.Create();
            rSA.FromXmlString(rsaString);

            //AES key exchange
            aes = Aes.Create();
            aes.Padding = PaddingMode.PKCS7;
            aes.KeySize = aesKeySize * 8;
            aes.GenerateKey();
            aes.GenerateIV();
            data = rSA.Encrypt(Encoding.UTF8.GetBytes(Convert.ToBase64String(aes.Key) + "|" + Convert.ToBase64String(aes.IV)), RSAEncryptionPadding.OaepSHA1);
            tcpClient.GetStream().Write(data, 0, data.Length);
            
            //time limit
            lastUpdate = DateTime.Now;
            isNotClosed = true;
            new Thread(offlineLimit).Start();

        }

        /// <summary>
        /// returns the tcp client instance of the user
        /// </summary>
        /// <returns></returns>
        public TcpClient GetTcpClient() { return this.tcpClient; }


        /// <summary>
        ///send encrypted bytes to the client
        /// </summary>
        public void WriteBytes(Byte[] data)
        {
            //for separation
            byte[] lenBytes = new byte[4];
            byte[] encrypted = aes.CreateEncryptor().TransformFinalBlock(data, 0, data.Length);
            BitConverter.GetBytes(encrypted.Length).CopyTo(lenBytes, 0);
            tcpClient.GetStream().Write(lenBytes, 0, 4);
            tcpClient.GetStream().Write(encrypted, 0, encrypted.Length);
        }

        

        /// <summary>
        ///read encrypted bytes from the client
        /// </summary>
        public byte[] ReadBytes()
        {

            byte[] lenBytes = new byte[4];
            int bytesRead = 0;

            // Keep reading until we actually have 4 bytes
            while (bytesRead < 4)
            {
                int n = tcpClient.GetStream().Read(lenBytes, bytesRead, 4 - bytesRead);
                if (n == 0) {
                    //client closed
                    this.Close();
                    return null;
                }
                bytesRead += n;
            }

            
            int num = BitConverter.ToInt32(lenBytes, 0);

            if (num == 0){
                //client closed
                this.Close();
                return null;
            }

            lastUpdate = DateTime.Now;

            byte[] data = new byte[num];
            bytesRead = 0;
            while (bytesRead < num)
            {
                int n1 = tcpClient.GetStream().Read(data, bytesRead, num- bytesRead);
                if (n1 == 0)
                {
                    //client closed
                    this.Close();
                    return null;
                }
                bytesRead += n1;
            }

            

            byte[] decrypted = aes.CreateDecryptor().TransformFinalBlock(data, 0, data.Length);
            return decrypted;
        }


        /// <summary>
        /// start reading
        /// </summary>
        public void StartReading(string username) {
            this.username = username;
            new Thread(() => { read(); }).Start();

        }

        /// <summary>
        /// read text from the client   
        /// </summary>
        private void read() {
            string request;
            while (isNotClosed) {
                try
                {
                    byte[] data = ReadBytes();
                    if (data != null)
                    {

                        request = Encoding.UTF8.GetString(data);

                        ClientRequestHandler.Handle(this, request);

                    }
                }
                catch(Exception ex) {
                    //when the client is disconneted
                    if (!(ex is IOException))
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    this.Close();
                }
                
            }
        }


        /// <summary>
        /// limit user offline session time
        /// </summary>
        private void offlineLimit()
        {
            while (isNotClosed) 
            {
                Thread.Sleep(30000);
                if (DateTime.Now.Subtract(lastUpdate).TotalMinutes > 10 )
                {
                    Close();
                }
            }
        }

        /// <summary>
        /// close the connection
        /// </summary
        public void Close()
        {
            Logging.Log(GetIp() + " disconnected or was disconnected");
            tcpClient.Close();
            isNotClosed = false;
            if (TwitterServerMain.connectedUsers.Contains(username)) {
                TwitterServerMain.connectedUsers.Remove(username);
            }
        }

        /// <summary>
        /// gets a string version of the ip
        /// </summary>
        /// <returns></returns>
        public string GetIp() {
            return ip;
        }

        /// <summary>
        /// gets the username of the client
        /// </summary>
        /// <returns></returns>
        public string GetUsername() {
            return this.username;
        }
        

    }
}
