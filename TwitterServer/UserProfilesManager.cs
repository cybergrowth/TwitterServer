using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;


namespace TwitterServer
{
    /// <summary>
    /// manages all user profile pictures
    /// </summary>
    static class UserProfilesManager
    {
        static string userProfilesFolder = Environment.CurrentDirectory + "\\userProfiles\\";
        static Dictionary<string, ImageFormat> FTypeConversion;

        static List<string> currrentProfiles= new List<string>();

        /// <summary>
        /// setup the user profile class
        /// </summary>
        public static void Setup() {
            System.IO.Directory.CreateDirectory(userProfilesFolder);
            currrentProfiles = Directory.GetFiles(userProfilesFolder).ToList<string>();
            FTypeConversion = new Dictionary<string, ImageFormat>();
            FTypeConversion.Add("png",ImageFormat.Png);
            FTypeConversion.Add("jpg",ImageFormat.Jpeg);
            FTypeConversion.Add("jpeg", ImageFormat.Jpeg);
        }
        /// <summary>
        /// add a profile picture
        /// </summary
        public static void addPicture(string pictureName,string fileType, byte[] data) {
            currrentProfiles.Add(pictureName);
            try
            {
                string[] current=Directory.GetFiles(userProfilesFolder, pictureName + ".*");
                for (int i = 0; i < current.Length; i++) {
                    File.Delete(current[i]);
                }
            }
            catch {//if there is no file or could not delete
            }
            
            using (MemoryStream ms = new MemoryStream(data))
            {
                Image image = Image.FromStream(ms);
                image.Save(userProfilesFolder+ pictureName + "." + fileType, FTypeConversion[fileType]);
            }
            
        }

        /// <summary>
        /// get a user profile picture with file type
        /// </summary
        public static (byte[],string) getUserProfile(string username) {
            string file = Directory.GetFiles(userProfilesFolder, username + ".*")[0];
            Image image = Image.FromFile( file);
            string Ftype = file.Split('.')[file.Split('.').Length - 1];


            using (var ms = new MemoryStream())
            {
                image.Save(ms, image.RawFormat);
                return (ms.ToArray(),Ftype);
            }
        }
        /// <summary>
        /// check if a user has a profile picture
        /// </summary
        public static bool hasImage(string pictureName)
        {
            return Directory.GetFiles(userProfilesFolder, pictureName + ".*").Length!=0;
        }
    }
}
