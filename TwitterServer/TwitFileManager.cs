using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TwitterServer
{
    /// <summary>
    /// manages all pictures of twits
    /// </summary>
    static class TwitFileManager
    {
        //the path of the twit pictures
        private static string path = Environment.CurrentDirectory + "\\TwitPictures\\";

        /// <summary>
        /// setup the class on start
        /// </summary>
        public static void Setup() {
            System.IO.Directory.CreateDirectory(path);
        }

        /// <summary>
        /// add a new picture
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public static void AddFile(string name, byte[] data) {
            using (FileStream fs= File.Create(path + name) )
            {
                fs.Write(data, 0, data.Length);
            }
        }

        /// <summary>
        /// remove all files of an id
        /// </summary>
        /// <param name="name"></param>
        public static void RemoveFileId(int id) {
            string[] current = Directory.GetFiles(path, id + ".*");
            for (int i = 0; i < current.Length; i++)
            {
                File.Delete(current[i]);
            }
        }

        /// <summary>
        /// gives you a picture by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static byte[] GetFile(string name) {
            return File.ReadAllBytes(path + name);
        }

        /// <summary>
        /// checks if a picture exists
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool FileExist(string name) {
            return File.Exists(path + name);
        }
    }
}
