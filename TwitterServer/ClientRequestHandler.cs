using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

namespace TwitterServer
{
    /// <summary>
    /// handles string requests from the client
    /// </summary>
    static class ClientRequestHandler
    {
        /// <summary>
        /// sorts each request to the right handle function
        /// </summary>
        /// <param name="user"></param>
        /// <param name="request"></param>
        public static void Handle(UserCommunication user, string request)
        {
            switch (request[0])
            {
                case '0':
                    TwitSearchRequest(user, request.Substring(1));
                    break;
                case '1':
                    NewTwitRequest(user, request.Substring(1));
                    break;
                case '2':
                    LikeRequest(user, request.Substring(1));
                    break;
                case '3':
                    DeleteTwitRequest(user, request.Substring(1));
                    break;
                case '4':
                    FollowUserRequest(user, request.Substring(1));
                    break;
                case '5':
                    GetUserPage(user, request.Substring(1));
                    break;
                case '6':
                    UserProfileHandle(user, request.Substring(1));
                    break;
                case '7':
                    SendFullTwit(user, request.Substring(1));
                    break;
                case '8':
                    ReplyRequest(user, request.Substring(1));
                    break;
                case '9':
                    twitAttachmentRequest(user, request.Substring(1));
                    break;
                case 'a':
                    UserSearchRequest(user, request.Substring(1));
                    break;

            }
        }

        /// <summary>
        /// respond to a search request
        /// </summary>
        private static void TwitSearchRequest(UserCommunication user, string request)
        {
            JsonDocument doc = JsonDocument.Parse(request);
            JsonElement root = doc.RootElement;
            int followed = root.GetProperty("followed").GetInt16();
            int maxId = root.GetProperty("maxId").GetInt32();
            string tags = root.GetProperty("tags").GetString();

            Logging.Log(user.GetTcpClient().Client.RemoteEndPoint.ToString() + " searched followed: " + followed + " with tags: " + tags);


            List<int> foundTwits = TwitSearcher.SearchTwitByTags(tags.Split(','), maxId, 7, followed == 1, user.GetUsername());



            JsonObject res = TwitListToJsonObject(foundTwits, user);


            user.WriteBytes(Encoding.UTF8.GetBytes("0" + res.ToJsonString()));
            Logging.Log("responded to " + user.GetTcpClient().Client.RemoteEndPoint.ToString() + "'s search request");
        }

        /// <summary>
        /// convert from a list of twits to json
        /// </summary>
        private static JsonObject TwitListToJsonObject(List<int> foundTwits, UserCommunication user)
        {
            JsonObject res = new JsonObject();
            for (int i = foundTwits.Count - 1; 0 <= i; i--)
            {//to make it so it is sorted by time - the newest will be on top
                JsonObject originaltwit = TwitsManager.GetTwit(foundTwits[i]);
                string Likes = originaltwit["Likes"].ToString();

                int replies = 0;
                if (!originaltwit["replies"].ToString().Equals(""))
                {
                    replies = originaltwit["replies"].ToString().Split(',').Count();
                }
                originaltwit["replies"] = replies;

                int n = Likes.Split(',').Length;

                if (Likes.Equals("")) { n = 0; }

                if (Likes.Split(',').Contains(user.GetUsername()))
                {
                    Likes = 1 + "" + n;
                }

                else { Likes = 0 + "" + n; }
                originaltwit["Likes"] = Likes;

                originaltwit["isonline"] = TwitterServerMain.connectedUsers.Contains(originaltwit["username"].ToString());
                res["" + foundTwits[i]] = originaltwit;
            }
            return res;
        }

        /// <summary>
        /// the user requested to make a new twit
        /// </summary>
        private static void NewTwitRequest(UserCommunication user, string request)
        {
            JsonDocument document = JsonDocument.Parse(request);
            JsonElement root = document.RootElement;
            string tags = root.GetProperty("tags").GetString();
            string content = root.GetProperty("content").GetString();
            string image = root.GetProperty("image").GetString();
            int id = TwitsManager.CreateTwit(user.GetUsername(), content, tags, Int16.Parse("" + image[0]));
            Logging.Log(user.GetIp() + " as " + user.GetUsername() + " created a new twit with id " + id);

            bool fail = true;
            //adjusting for latency
            Thread.Sleep(150);
            if (image[0] == '1')
            {
                int length = Int32.Parse(image.Substring(1));
                byte[] data = user.ReadBytes();
                if (data.Length <= Math.Pow(10, 7) * 2)
                {
                    TwitFileManager.AddFile(id + ".png", data);
                    fail = false;
                }
                
            }
            else if (image[0] == '2')
            {
                int length = Int32.Parse(image.Substring(1));
                Thread.Sleep(10);
                byte[] data = user.ReadBytes();
                if (data.Length <= Math.Pow(10, 7) * 2)
                {
                    TwitFileManager.AddFile(id + ".mp4", data);
                    fail = false;
                }
            }
            else
            {
                fail = false;
            }

            if (fail)
            {
                user.WriteBytes(Encoding.UTF8.GetBytes("6Maximum file size 20MB"));
                Logging.Log(user.GetTcpClient().Client.RemoteEndPoint.ToString() + "tried to add an attachment to a twit but it was too large");
            }
        }

        /// <summary>
        /// handle a like request
        /// </summary>
        private static void LikeRequest(UserCommunication user, string request)
        {

            int twitId = Int32.Parse(request.Substring(1));//maybe return if twit DNE and log
            if (request[0] == '0')
            {
                bool success = TwitsManager.LikeTwit(twitId, user.GetUsername());
                if (success)
                {
                    Logging.Log(user.GetIp() + " as " + user.GetUsername() + " liked twit " + twitId);
                }
                else
                {
                    user.WriteBytes(Encoding.UTF8.GetBytes("6twit or reply does not exist anymore"));
                    Logging.Log(user.GetIp() + " as " + user.GetUsername() + " tried to like twit " + twitId + " but it does not exist");
                }
            }
            else
            {
                bool success = TwitsManager.RemoveLikeTwit(twitId, user.GetUsername());
                if (success)
                {
                    Logging.Log(user.GetIp() + " as " + user.GetUsername() + " unliked twit " + twitId);
                }
                else
                {
                    user.WriteBytes(Encoding.UTF8.GetBytes("6twit or reply does not exist anymore"));
                    Logging.Log(user.GetIp() + " as " + user.GetUsername() + " tried to like twit " + twitId + " but it does not exist");
                }
            }
        }

        /// <summary>
        /// delete twit
        /// </summary>
        private static void DeleteTwitRequest(UserCommunication user, string request)
        {
            int id;
            if (Int32.TryParse(request, out id))
            {
                JsonNode twit;
                if (id >= 0)
                {//twit
                    twit = TwitsManager.GetTwit(id);
                }
                else
                {
                    //reply
                    twit = TwitsManager.GetReply(-id - 1);
                }
                if (twit != null)
                {
                    string permittedUser = twit["username"].GetValue<string>();
                    if (permittedUser.Equals(user.GetUsername()))
                    {
                        TwitsManager.RemoveTwit(id, true);
                        Logging.Log(user.GetTcpClient().Client.RemoteEndPoint.ToString() + " requested to delete twit " + id);
                    }
                    else
                    {
                        Logging.Log(user.GetTcpClient().Client.RemoteEndPoint.ToString() + " requested to delete twit " + id + " but he is not permited");
                    }
                }
                else
                {
                    user.WriteBytes(Encoding.UTF8.GetBytes("6twit does not exist anymore"));
                    Logging.Log(user.GetTcpClient().Client.RemoteEndPoint.ToString() + " requested to delete twit " + id + " but it does not exist anymore");
                }

            }
        }

        /// <summary>
        /// follow user request
        /// </summary>
        private static void FollowUserRequest(UserCommunication user, string request)
        {
            string target = request.Substring(1);

            if (UsersSqlHandler.IsUserNameAvailable(target))
            {
                //username does not exist
                Logging.Log(user.GetIp() + " as " + user.GetUsername() + " tried to follow " + target + " but it does not exist");
                return;
            }

            if (request[0] == '0')
            {
                UsersSqlHandler.followUser(user.GetUsername(), target);
                Logging.Log(user.GetIp() + " as " + user.GetUsername() + " tried to follow " + target);
            }
            else
            {
                UsersSqlHandler.unFollowUser(user.GetUsername(), target);
                Logging.Log(user.GetIp() + " as " + user.GetUsername() + " tried to unfollow " + target);
            }
        }

        /// <summary>
        /// handle userpage request
        /// </summary>
        private static void GetUserPage(UserCommunication user, string request)
        {
            JsonDocument document = JsonDocument.Parse(request);
            JsonElement root = document.RootElement;
            string target = root.GetProperty("user").GetString();
            int maxId = root.GetProperty("maxId").GetInt32();

            if (UsersSqlHandler.IsUserNameAvailable(target)) {
                //username does not exist
                Logging.Log(user.GetIp() + " as " + user.GetUsername() + " requested the user page of " + target+" but it does not exist");
                return;
            }

            string[] followedBy = UsersSqlHandler.getUserFollowedBy(target);
            int isFollowed = 0;
            foreach (string f in followedBy)
            {
                if (f.Equals(user.GetUsername()))
                {
                    isFollowed = 1;
                    break;
                }
            }
            int followers = 0;
            if (followedBy[0] != "")
            {
                followers = followedBy.Length;
            }

            List<int> twitIds = TwitSearcher.SearchTwitByUser(target, maxId, 7);
            JsonObject twits = TwitListToJsonObject(twitIds, user);

            JsonObject res = new JsonObject()
            {
                ["isfollowed"] = isFollowed,
                ["followers"] = followers,
                ["twits"] = twits
            };//twits...

            user.WriteBytes(Encoding.UTF8.GetBytes("1" + res.ToJsonString()));
            Logging.Log(user.GetIp() + " as " + user.GetUsername() + " requested the user page of " + target);
        }


        /// <summary>
        /// request to update profile picture or get one
        /// </summary>
        private static void UserProfileHandle(UserCommunication user, string request)
        {
            //need to add image checks
            if (request[0] == '0')
            {
                //add profile picture
                string fType = request.Substring(1);
                byte[] imageBytes = user.ReadBytes();
                if (new string[3] { "png", "jpg", "jpeg" }.Contains(fType))
                {
                    if (imageBytes.Length <= Math.Pow(10, 7) * 2)
                    {
                        UserProfilesManager.addPicture(user.GetUsername(), fType, imageBytes);
                        Logging.Log(user.GetTcpClient().Client.RemoteEndPoint.ToString() + " added a new profile picture");
                    }
                    else
                    {
                        user.WriteBytes(Encoding.UTF8.GetBytes("6Maximum file size 20MB"));
                        Logging.Log(user.GetTcpClient().Client.RemoteEndPoint.ToString() + "tried to add a new profile picture but it was too large");
                    }
                }
                else
                {
                    //invalid image format
                    user.WriteBytes(Encoding.UTF8.GetBytes("6Invalid image format"));
                    Logging.Log(user.GetTcpClient().Client.RemoteEndPoint.ToString() + " added a new profile picture but the format was invalid");
                }
            }
            else
            {
                //get
                string target = request.Substring(1);
                if (UserProfilesManager.hasImage(target))
                {
                    (byte[], string) imgData = UserProfilesManager.getUserProfile(target);
                    lock (user.GetTcpClient().GetStream())
                    {
                        user.WriteBytes(Encoding.UTF8.GetBytes("2" + imgData.Item2 + ":" + target));
                        user.WriteBytes(imgData.Item1);
                    }
                    Logging.Log(user.GetTcpClient().Client.RemoteEndPoint.ToString() + " asked for " + target + "'s profile");
                }
                else
                {
                    Logging.Log(user.GetTcpClient().Client.RemoteEndPoint.ToString() + " asked for " + target + "'s profile but it does not exist");
                }

            }
        }


        /// <summary>
        /// when a client wants both twit and replies
        /// </summary>
        /// <param name="user"></param>
        /// <param name="request"></param>
        private static void SendFullTwit(UserCommunication user, string request)
        {
            int id = Int32.Parse(request);
            JsonObject twit = TwitsManager.GetFullTwit(id, user.GetUsername());
            if (twit != null)
            {
                user.WriteBytes(Encoding.UTF8.GetBytes("3" + twit));
                Logging.Log(user.GetIp() + " as " + user.GetUsername() + " requested full twit " + id);
            }
            else
            {
                //twit deleted by the time the user clicked
                user.WriteBytes(Encoding.UTF8.GetBytes("6twit does not exist anymore"));
                Logging.Log(user.GetIp() + " as " + user.GetUsername() + " requested full twit " + id + " but it was already deleted");
            }
        }

        /// <summary>
        /// user requests to reply to a reply
        /// </summary>
        /// <param name="user"></param>
        /// <param name="request"></param>
        private static void ReplyRequest(UserCommunication user, string request)
        {
            JsonDocument jsonDocument = JsonDocument.Parse(request);
            JsonElement root = jsonDocument.RootElement;
            int id = Int32.Parse(root.GetProperty("id").ToString());
            string content = root.GetProperty("content").ToString();
            string image = root.GetProperty("image").ToString();
            int newId = TwitsManager.replyToTwit(user.GetUsername(), id, content, Int32.Parse("" + image[0]));
            if (newId != -1)
            {
                if (id > 0)
                {
                    Logging.Log(user.GetIp() + " as " + user.GetUsername() + " replied to twit " + id);
                }
                else
                {
                    Logging.Log(user.GetIp() + " as " + user.GetUsername() + " replied to reply " + -(id + 1));
                }
                user.WriteBytes(Encoding.UTF8.GetBytes("4" + newId + ":" + image[0]));
            }
            else
            {
                //twit or reply does not exist anymore
                Logging.Log(user.GetIp() + " as " + user.GetUsername() + " tried to reply to twit " + id + " but it does not exist anymore");
                user.WriteBytes(Encoding.UTF8.GetBytes("6twit or reply does not exist anymore"));
            }

            bool fail = true;
            if (image[0] == '1')
            {
                int length = Int32.Parse(image.Substring(1));
                byte[] data = user.ReadBytes();
                if (data.Length <= Math.Pow(10, 7) * 2)
                {
                    TwitFileManager.AddFile((-newId - 1) + ".png", data);
                    fail = false;
                }
            }
            else if (image[0] == '2')
            {
                int length = Int32.Parse(image.Substring(1));
                byte[] data = user.ReadBytes();
                if (data.Length <= Math.Pow(10, 7) * 2)
                {
                    TwitFileManager.AddFile((-newId - 1) + ".mp4", data);
                    fail = false;
                }
            }
            else
            {
                fail=false;
            }

            if (fail)
            {
                user.WriteBytes(Encoding.UTF8.GetBytes("6Maximum file size 20MB"));
                Logging.Log(user.GetTcpClient().Client.RemoteEndPoint.ToString() + "tried to add an attachment to a reply but it was too large");
            }
        }


        /// <summary>
        /// get twitAttachment
        /// </summary>
        /// <param name="user"></param>
        /// <param name="request"></param>
        private static void twitAttachmentRequest(UserCommunication user, string request)
        {
            int attachmentType = Int32.Parse(request[0] + "");
            string id = request.Substring(1);
            string name = id;
            if (attachmentType == 1)
            {
                name += ".png";
            }
            else { name += ".mp4"; }

            if (TwitFileManager.FileExist(name))
            {//add for videos and isExist
                lock (user.GetTcpClient().GetStream())
                {
                    user.WriteBytes(Encoding.UTF8.GetBytes("5" + attachmentType + name));
                    user.WriteBytes(TwitFileManager.GetFile(name));
                }

                Logging.Log(user.GetTcpClient().Client.RemoteEndPoint.ToString() + " asked for twit " + id + "'s attachment");
            }
            else
            {
                Logging.Log(user.GetIp() + " as " + user.GetUsername() + " requested twit " + id + " attachment but it does not exist anymore");
            }
        }

        /// <summary>
        /// return a user search request
        /// </summary>
        /// <param name="user"></param>
        /// <param name="request"></param>
        private static void UserSearchRequest(UserCommunication user, string request)
        {
            List<string> searchedUsers = TwitSearcher.SearchUserByUsername(request.Substring(1));
            if (request[0] == '1')
            {
                searchedUsers = TwitSearcher.SortUsersForFollowed(user.GetUsername(), searchedUsers);
            }
            JsonObject res = new JsonObject()
            {
                ["users"] = JsonArray.Parse(JsonSerializer.Serialize(searchedUsers))
            };
            user.WriteBytes(Encoding.UTF8.GetBytes("7" + res.ToJsonString()));
            Logging.Log(user.GetIp() + " as " + user.GetUsername() + " searched users with query \"" + request.Substring(1) + "\" with followers parameter " + request[0]);
        }

    }
}
