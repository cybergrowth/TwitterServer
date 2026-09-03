using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace TwitterServer
{

    /// <summary>
    /// handles all twit creating and replying 
    /// </summary>
    static class TwitsManager
    {
        private static string connectString;
        private static SqlConnection sqlConnection;
        private static SqlCommand sqlCommand;

        //IMPORTANT NOTE: Dual used functions for both twits and replies diffrentiate between them using (-id-1) as input if id is for a reply

        /// <summary>
        /// setups the class on start
        /// </summary>
        /// <param name="connectString"></param>
        public static void Setup(string connectString)
        {
            TwitsManager.connectString = connectString;
            sqlConnection = new SqlConnection(connectString);
            sqlCommand = sqlConnection.CreateCommand();
        }

        /// <summary>
        /// creates a new twit
        /// </summary>
        /// <param name="username"></param>
        /// <param name="content"></param>
        /// <param name="tags"></param>
        /// <param name="image"></param>
        /// <returns></returns>
        public static int CreateTwit(string username, string content, string tags,int image)
        {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    int id = GetNewTwitId();

                    sqlCommand.CommandText = "INSERT INTO Twits (Id,Username,Content,Time,Likes,Tags,Replies,Image) VALUES(@id,@username,@content,'" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "','',@tags,''," + image + ");";
                    sqlCommand.Parameters.AddWithValue("id", id);
                    sqlCommand.Parameters.AddWithValue("username", username);
                    sqlCommand.Parameters.AddWithValue("@content", content);
                    sqlCommand.Parameters.AddWithValue("tags", tags);
                    sqlConnection.Open();
                    sqlCommand.ExecuteNonQuery();
                    sqlCommand.Parameters.Clear();
                    sqlConnection.Close();

                    return id;
                }
            }
        }

        /// <summary>
        /// gives you the next available twit id
        /// </summary>
        /// <returns></returns>
        private static int GetNewTwitId()
        {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    sqlCommand.CommandText = "SELECT ISNULL(MAX(Id), -1) FROM Twits;";
                    sqlConnection.Open();
                    int max = (int)sqlCommand.ExecuteScalar();
                    sqlConnection.Close();
                    return max + 1;
                }
            }
        }

        /// <summary>
        /// returns a twit or a reply as a json object
        /// </summary>
        /// <param name="TwitId"></param>
        /// <returns></returns>
        public static JsonObject GetTwit(int TwitId)
        {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    sqlConnection.Open();
                    //check if exists
                    if (!isExist(TwitId, "Twits"))
                    {
                        sqlConnection.Close();
                        return null;
                    }

                    //get twit
                    sqlCommand.CommandText = "SELECT * FROM Twits WHERE Id=@TwitId;";
                    sqlCommand.Parameters.AddWithValue("TwitId", TwitId);
                    SqlDataReader dataReader = sqlCommand.ExecuteReader();
                    dataReader.Read();

                    JsonObject res = new JsonObject()
                    {
                        ["id"] = dataReader.GetInt32(0),
                        ["username"] = dataReader.GetString(1),
                        ["content"] = dataReader.GetString(2),
                        ["time"] = dataReader.GetString(3),
                        ["Likes"] = dataReader.GetString(4),
                        ["tags"] = dataReader.GetString(5),
                        ["replies"] = dataReader.GetString(6),
                        ["image"] = dataReader.GetInt16(7)
                    };

                    sqlCommand.Parameters.Clear();
                    sqlConnection.Close();
                    return res;
                }
            }
        }

        /// <summary>
        /// removes a twit
        /// </summary>
        /// <param name="TwitId"></param>
        /// <param name="first"></param>
        public static void RemoveTwit(int TwitId, bool first)
        {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    string replies;
                    TwitFileManager.RemoveFileId(TwitId);
                    sqlConnection.Open();
                    if (TwitId >= 0)
                    {
                        if (!isExist(TwitId, "Twits"))
                        {
                            sqlConnection.Close();
                            return;
                        }
                        sqlConnection.Close();
                        replies = GetTwit(TwitId)["replies"].GetValue<string>();
                        sqlCommand.CommandText = "DELETE FROM Twits WHERE Id=@TwitId";
                        sqlCommand.Parameters.AddWithValue("TwitId", TwitId);
                    }
                    else
                    {
                        if (!isExist(-TwitId - 1, "Replies"))
                        {
                            sqlConnection.Close();
                            return;
                        }
                        if (first)
                        {
                            RemoveFromParent(-TwitId - 1);
                        }
                        sqlConnection.Close();
                        replies = GetReply(-TwitId - 1)["replies"].GetValue<string>();
                        sqlCommand.CommandText = "DELETE FROM Replies WHERE Id=@TwitId";
                        sqlCommand.Parameters.AddWithValue("TwitId", -TwitId - 1);

                    }
                    sqlConnection.Open();
                    sqlCommand.ExecuteNonQuery();
                    sqlConnection.Close();
                    if (!replies.Equals(""))
                    {
                        foreach (string repId in replies.Split(','))
                        {
                            RemoveTwit(-(Int32.Parse(repId) + 1), false);
                        }
                    }
                    sqlCommand.Parameters.Clear();
                }
            }
        }

        /// <summary>
        /// removes a reply from its parents data
        /// </summary>
        /// <param name="repId"></param>
        private static void RemoveFromParent(int repId)
        {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    sqlCommand.CommandText = "SELECT Parent FROM Replies WHERE Id=@repId";
                    sqlCommand.Parameters.AddWithValue("repId", repId);
                    int parent = (int)sqlCommand.ExecuteScalar();
                    string tableName = "Twits";
                    if (parent < 0)
                    {
                        tableName = "Replies";
                        parent = -parent - 1;
                    }

                    sqlCommand.CommandText = "SELECT Replies FROM " + tableName + " WHERE Id= @parent";
                    sqlCommand.Parameters.AddWithValue("parent", parent);

                    string reps = (string)sqlCommand.ExecuteScalar();

                    string[] replies = reps.Split(',');
                    string res = "";
                    if (!replies[0].Equals(repId.ToString()))
                    {
                        for (int i = 0; i < replies.Length; i++)
                        {
                            if (!replies[i].Equals(repId.ToString()))
                            {
                                res += replies[i];
                                if (i < replies.Length - 1)
                                    res += ",";
                            }
                            else
                            {
                                if (i == replies.Length - 1)
                                {
                                    res = res.Remove(res.Length - 1);
                                }
                            }
                        }
                    }
                    sqlCommand.CommandText = "UPDATE " + tableName + " SET Replies=@replies WHERE Id= @parent";
                    sqlCommand.Parameters.AddWithValue("replies", res);
                    sqlCommand.ExecuteNonQuery();
                    sqlCommand.Parameters.Clear();
                }
            }
        }

        /// <summary>
        /// a request to like a twit
        /// </summary>
        /// <param name="twitId"></param>
        /// <param name="liker"></param>
        /// <returns>if operation is successful</returns>
        public static bool LikeTwit(int twitId, string liker)
        {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    //replies and twits
                    string parentBase = "Twits";
                    if (twitId < 0)
                    {
                        twitId = -(twitId + 1);
                        parentBase = "Replies";
                    }
                    //check if exists
                    sqlConnection.Open();
                    if (!isExist(twitId, parentBase))
                    {
                        sqlConnection.Close();
                        return false;
                    }

                    sqlCommand.CommandText = "SELECT Likes FROM " + parentBase + " WHERE Id= @twitId";
                    sqlCommand.Parameters.AddWithValue("twitId", twitId);
                    SqlDataReader dataReader = sqlCommand.ExecuteReader();
                    dataReader.Read();
                    string likes = dataReader.GetString(0);
                    dataReader.Close();
                    bool change = true;
                    if (likes == "")
                    {
                        likes = liker;
                    }
                    else if (!likes.Split(',').Contains(liker))
                    {
                        likes += "," + liker;
                    }
                    else { change = false; }

                    if (change)
                    {
                        sqlCommand.CommandText = "UPDATE " + parentBase + " SET Likes= @likes  WHERE Id= @twitId ;";
                        sqlCommand.Parameters.AddWithValue("likes", likes);
                        sqlCommand.ExecuteNonQuery();
                    }
                    sqlConnection.Close();
                    sqlCommand.Parameters.Clear();
                    return true;
                }
            }
        }

        /// <summary>
        /// a request to unlike a twit
        /// </summary>
        /// <param name="twitId"></param>
        /// <param name="Liker"></param>
        /// <returns>if operation is successful</returns>
        public static bool RemoveLikeTwit(int twitId, string Liker)
        {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    //for replies
                    string parentBase = "Twits";
                    if (twitId < 0)
                    {
                        twitId = -(twitId + 1);
                        parentBase = "Replies";
                    }

                    sqlConnection.Open();
                    //check if exists
                    if (!isExist(twitId, parentBase))
                    {
                        sqlConnection.Close();
                        return false;
                    }

                    sqlCommand.CommandText = "SELECT Likes FROM " + parentBase + " WHERE Id= @twitId";
                    sqlCommand.Parameters.AddWithValue("twitId", twitId);
                    SqlDataReader dataReader = sqlCommand.ExecuteReader();
                    dataReader.Read();
                    string[] likes = dataReader.GetString(0).Split(',');
                    dataReader.Close();
                    bool change = false;
                    string res = "";
                    if (likes.Length == 0) { }
                    else if (likes.Length == 1)
                    {
                        if (likes[0].Equals(Liker))
                        {
                            change = true;
                            res = "";
                        }
                    }
                    else
                    {
                        for (int i = 0; i < likes.Length; i++)
                        {
                            if (!likes[i].Equals(Liker))
                            {
                                res += likes[i];
                                if (i < likes.Length - 1)
                                    res += ",";
                            }
                            else
                            {
                                change = true;

                                if (i == likes.Length - 1)
                                {
                                    res = res.Remove(res.Length - 1);
                                }
                            }
                        }
                    }
                    if (change)
                    {
                        sqlCommand.CommandText = "UPDATE " + parentBase + " SET Likes=@likes WHERE Id= @twitId ;";
                        sqlCommand.Parameters.AddWithValue("likes", res);
                        sqlCommand.ExecuteNonQuery();
                    }
                    sqlCommand.Parameters.Clear();
                    sqlConnection.Close();
                    return true;
                }
            }
        }

        /// <summary>
        /// replies- for replying to reply you do -Id-1 #need to add reply to reply
        /// </summary>
        public static int replyToTwit(string username, int twitId, string content, int image)
        {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    //choose the database of the parent
                    string parentbase = "Twits";
                    int orID = twitId;
                    if (twitId < 0)
                    {
                        parentbase = "Replies";
                        twitId = -(twitId + 1);
                    }
                    //check if exists
                    sqlConnection.Open();
                    if (!isExist(twitId, parentbase))
                    {
                        sqlConnection.Close();
                        return -1;
                    }

                    //get current replies
                    int id = GetNewReplyId();
                    sqlCommand.CommandText = "SELECT Replies FROM " + parentbase + " WHERE Id= @twitId";
                    sqlCommand.Parameters.AddWithValue("twitId", twitId);
                    SqlDataReader dataReader = sqlCommand.ExecuteReader();
                    dataReader.Read();
                    string replies = dataReader.GetString(0);
                    dataReader.Close();

                    //updating in database
                    if (replies.Equals(""))
                    {
                        replies += id;
                    }
                    else
                    {
                        replies += "," + id;
                    }
                    sqlCommand.CommandText = "Update " + parentbase + " SET Replies= @replies WHERE Id=@twitId";
                    sqlCommand.Parameters.AddWithValue("replies", replies);
                    sqlCommand.ExecuteNonQuery();

                    //creating the reply
                    sqlCommand.CommandText = "INSERT INTO Replies (Id,Username,Content,Time,Likes,Replies,Parent,Image) VALUES( @id , @username ,@content ,'" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "','','',@orID,@image);";
                    sqlCommand.Parameters.AddWithValue("id", id);
                    sqlCommand.Parameters.AddWithValue("username", username);
                    sqlCommand.Parameters.AddWithValue("content", content);
                    sqlCommand.Parameters.AddWithValue("orID", orID);
                    sqlCommand.Parameters.AddWithValue("image", image);

                    sqlCommand.ExecuteNonQuery();
                    sqlConnection.Close();
                    sqlCommand.Parameters.Clear();
                    return id;
                }
            }
        }

        /// <summary>
        /// gives you the next available reply id
        /// </summary>
        /// <returns></returns>
        private static int GetNewReplyId()
        {
            lock (sqlConnection)
            {
                lock (sqlConnection)
                {
                    sqlCommand.CommandText = "SELECT ISNULL(MAX(Id), -1) FROM Replies;";
                    int max = (int)sqlCommand.ExecuteScalar();
                    return max + 1;
                }
            }
        }

        /// <summary>
        /// returns a reply
        /// </summary>
        /// <param name="ReplyId"></param>
        /// <returns></returns>
        public static JsonObject GetReply(int ReplyId)
        {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    sqlConnection.Open();
                    //check if exists
                    if (!isExist(ReplyId, "Replies"))
                    {
                        Console.WriteLine(ReplyId);
                        sqlConnection.Close();
                        return null;
                    }

                    //get reply
                    sqlCommand.CommandText = "SELECT * FROM Replies WHERE Id= @ReplyId ;";
                    sqlCommand.Parameters.AddWithValue("ReplyId", ReplyId);
                    SqlDataReader dataReader = sqlCommand.ExecuteReader();
                    dataReader.Read();

                    JsonObject res = new JsonObject
                    {
                        ["id"] = dataReader.GetInt32(0),
                        ["username"] = dataReader.GetString(1),
                        ["time"] = dataReader.GetString(2),
                        ["content"] = dataReader.GetString(3),
                        ["likes"] = dataReader.GetString(4),
                        ["replies"] = dataReader.GetString(5),
                        ["image"] = dataReader.GetInt16(7)
                    };
                    sqlConnection.Close();
                    sqlCommand.Parameters.Clear();
                    return res;
                }
            }
        }

        /// <summary>
        /// Getting a full twits data with all replies inside an array
        /// </summary>
        public static JsonObject GetFullTwit(int id, string requesterName)
        {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    JsonObject mainTwit = GetTwit(id);
                    if (mainTwit == null) { return null; }
                    string[] reps = mainTwit["replies"].ToString().Split(',');
                    JsonArray replies;
                    if (reps[0].Equals(""))
                        replies = new JsonArray();
                    else
                        replies = repObj(reps, requesterName);

                    //likes
                    int countLikes;
                    string[] likes = mainTwit["Likes"].ToString().Split(',');
                    if (likes[0].Equals("")) { countLikes = 0; }
                    else
                    {
                        countLikes = likes.Length;
                    }

                    JsonObject res = new JsonObject()
                    {
                        ["id"] = mainTwit["id"].ToString(),
                        ["username"] = mainTwit["username"].ToString(),
                        ["isonline"] = TwitterServerMain.connectedUsers.Contains(mainTwit["username"].ToString()),
                        ["content"] = mainTwit["content"].ToString(),
                        ["time"] = mainTwit["time"].ToString(),
                        ["likes"] = "" + countLikes,
                        ["hasLiked"] = likes.Contains(requesterName),
                        ["tags"] = mainTwit["tags"].ToString(),
                        ["image"] = mainTwit["image"].ToString()
                        ,
                        ["replies"] = replies
                    };


                    return res;
                }
            }
        }

        /// <summary>   
        /// A recursive function which hatches out all replies as a JsonArray
        /// </summary>
        private static JsonArray repObj(string[] replies, string requesterName)
        {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    JsonArray arr = new JsonArray();
                    JsonObject temp;
                    JsonArray subReps;
                    string[] subRepStr;
                    for (int i = 0; i < replies.Length; i++)
                    {
                        JsonObject rep = GetReply(Int32.Parse(replies[i]));

                        subRepStr = rep["replies"].ToString().Split(',');
                        if (subRepStr[0].Equals(""))
                            subReps = new JsonArray();
                        else
                            subReps = repObj(subRepStr, requesterName);

                        //likes
                        int countLikes;
                        string[] likes = rep["likes"].ToString().Split(',');
                        if (likes[0].Equals("")) { countLikes = 0; }
                        else
                        {
                            countLikes = likes.Length;
                        }

                        temp = new JsonObject()
                        {
                            ["id"] = rep["id"].ToString(),
                            ["username"] = rep["username"].ToString(),
                            ["isonline"] = TwitterServerMain.connectedUsers.Contains(rep["username"].ToString()),
                            ["time"] = rep["time"].ToString(),
                            ["content"] = rep["content"].ToString(),
                            ["likes"] = countLikes,
                            ["hasLiked"] = likes.Contains(requesterName),
                            ["image"] = rep["image"].ToString(),
                            ["replies"] = subReps
                        };
                        arr.Add(temp);
                    }

                    return arr;
                }
            }
        }

        /// <summary>
        /// checking if a twit or reply exists with and id in a given table
        /// </summary>
        /// <param name="id"></param>
        /// <param name="parentBase"></param>
        /// <returns></returns>
        private static bool isExist(int id, string parentBase)
        {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    sqlCommand.CommandText = "SELECT COUNT(*) FROM " + parentBase + " WHERE Id=" + id;
                    int count = (int)sqlCommand.ExecuteScalar();
                    return count == 1;
                }
            }
        }
    }
}
