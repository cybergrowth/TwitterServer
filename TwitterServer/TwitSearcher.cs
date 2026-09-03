using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TwitterServer
{

    /// <summary>
    /// handles all searchers
    /// </summary>
    static class TwitSearcher
    {
        private static SqlConnection sqlConnection;
        private static SqlCommand sqlCommand;

        /// <summary>
        /// search setup
        /// </summary>
        public static void Setup(string sqlPath)
        {
            sqlConnection = new SqlConnection(sqlPath);
            sqlCommand = sqlConnection.CreateCommand();
        }

        /// <summary>
        /// search by tags
        /// </summary>
        public static List<int> SearchTwitByTags(string[] tags,int maxId,int searchAmount,bool onlyFollowed,string user)
        {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    List<int> res = new List<int>();
                    Dictionary<int, string> TwitList = new Dictionary<int, string>();
                    sqlConnection.Open();
                    sqlCommand.CommandText = "SELECT Id,Tags FROM TWITS;";
                    SqlDataReader reader = sqlCommand.ExecuteReader();

                    //getting twit data
                    int id; string tagString;
                    while (reader.Read())
                    {
                        id = reader.GetInt32(0);
                        tagString = reader.GetString(1);
                        TwitList.Add(id, tagString);
                    }
                    sqlConnection.Close();

                    //checking for tags
                    string[] valueTags; bool include;
                    foreach (int KeyId in TwitList.Keys.Reverse())
                    {


                        valueTags = TwitList[KeyId].Split(',');
                        include = true;
                        for (int i = 0; i < tags.Length; i++)
                        {
                            if (!valueTags.Contains(tags[i]) && !tags[i].Equals(""))
                            {
                                include = false;
                                i = tags.Length - 1;
                            }
                        }
                        if (include && (maxId == -1 || maxId > KeyId))
                        {
                            res.Add(KeyId);
                        }
                    }

                    //sort for followers
                    if (onlyFollowed)
                    {
                        res = SortTwitsForFollowers(user, res);
                    }

                    //sort for amount
                    List<int> newRes = new List<int>();
                    for (int i = 0; i < Math.Min(res.Count, searchAmount); i++)
                    {
                        newRes.Add(res[i]);
                    }
                    newRes.Reverse();
                    return newRes;
                }
            }
        }


        /// <summary>
        /// sort only for followers
        /// </summary>
        private static List<int> SortTwitsForFollowers(string user,List<int> originalTwits) {
            string[] following=UsersSqlHandler.getUserFollowing(user);
            Dictionary<int,string> twitUsers=new Dictionary<int, string>();
            List<int> res=new List<int>();
            for (int i = 0; i < originalTwits.Count; i++) {
                twitUsers[originalTwits[i]] = TwitsManager.GetTwit(originalTwits[i])["username"].GetValue<string>();
            }
            foreach (int key in twitUsers.Keys) {
                if (following.Contains(twitUsers[key])) {
                    res.Add(key);
                }
            }
            return res;
        }


        /// <summary>
        /// get all twits for a username
        /// </summary>
        public static List<int> SearchTwitByUser(string user,int maxId,int searchAmount) {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    sqlCommand.CommandText = "SELECT Id FROM Twits WHERE username='" + user + "'";
                    sqlConnection.Open();
                    List<int> res = new List<int>();
                    SqlDataReader reader = sqlCommand.ExecuteReader();
                    int temp;
                    while (reader.Read())
                    {
                        temp = reader.GetInt32(0);
                        if (maxId == -1 || maxId > temp)
                        {
                            res.Add(temp);
                        }
                    }
                    sqlConnection.Close();
                    res.Reverse();
                    List<int> newRes = new List<int>();
                    for (int i = 0; i < Math.Min(res.Count, searchAmount); i++)
                    {
                        newRes.Add(res[i]);
                    }
                    newRes.Reverse();
                    return newRes;
                }
            }
        }

        /// <summary>
        /// allowes you take any part of a username and find it for example -> search coin you can get coinmain or coinmaster...
        /// </summary>
        /// <param name="username"></param>
        /// <returns>list of found usernames</returns>
        public static List<string> SearchUserByUsername(string username) {
            lock (sqlConnection)
            {
                lock (sqlCommand)
                {
                    List<string> res = new List<string>();
                    sqlCommand.CommandText = "SELECT username FROM users";
                    sqlConnection.Open();
                    SqlDataReader reader = sqlCommand.ExecuteReader();
                    List<string> users = new List<string>();
                    while (reader.Read())
                    {
                        users.Add(reader.GetString(0));
                    }
                    sqlConnection.Close();


                    string curUser;
                    bool corresponds;
                    for (int i = 0; i < users.Count; i++)
                    {
                        corresponds = true;
                        curUser = users[i];
                        if (username.Length <= curUser.Length)
                        {
                            for (int j = 0; j < username.Length; j++)
                            {
                                if (username[j] != curUser[j])
                                {
                                    corresponds = false;
                                    break;
                                }
                            }
                        }
                        else { corresponds = false; }
                        if (corresponds)
                        {
                            res.Add(curUser);
                        }
                    }

                    return res;
                }
            }
        }

        /// <summary>
        /// sorts a list of users to only the ones who a user follows
        /// </summary>
        /// <param name="username"></param>
        /// <param name="users"></param>
        /// <returns></returns>
        public static List<string> SortUsersForFollowed(string username, List<string> users) {
            string[] following = UsersSqlHandler.getUserFollowing(username);
            List<string> res=new List<string>();
            for (int i = 0; i < users.Count; i++) {
                if (following.Contains(users[i])) {
                    res.Add(users[i]);
                }
            }
            return res;
        }
    }
}
