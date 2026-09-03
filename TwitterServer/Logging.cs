using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TwitterServer
{
    /// <summary>
    /// the class is responcible for all the logging
    /// </summary>
    static class Logging
    {
        static string logFile = Environment.CurrentDirectory+"\\logs\\"+DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") +".txt";
        static Queue<string> logQueue = new Queue<string>();
        /// <summary>
        /// main logging thread which checks if there is new stuff to log
        /// </summary>
        static Logging()
        {
            new Thread(() =>
            {
                System.IO.Directory.CreateDirectory(Environment.CurrentDirectory + "\\logs");

                using (FileStream file = File.Create(logFile)) ;

                string toLog;
                while (true)
                {
                    if (logQueue.Count > 0)
                    {
                        using (StreamWriter writer = new StreamWriter(logFile,true))
                        {
                            toLog = logQueue.Dequeue();
                            writer.WriteLine(DateTime.Now.ToShortTimeString()+" : "+ toLog);
                            Console.WriteLine(toLog);
                        }
                    }
                    Thread.Sleep(10);
                }
                
            }).Start();
        }
        /// <summary>
        /// logs a message
        /// </summary>
        /// <param name="message"></param>
        public static void Log(string message) {
            logQueue.Enqueue(message);
        }    

    }
}
