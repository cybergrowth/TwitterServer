using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TwitterServer
{

    /// <summary>
    /// handles the users sql database
    /// </summary>
    internal static class UsersSqlHandler
    {
        private static SqlConnection connection;
        private static SqlCommand command;

        /// <summary>
        /// setup the class on start(must be run first)
        /// </summary>
        /// <param name="connectString"></param>
        public static void Setup(string connectString)
        {
            connection = new SqlConnection(connectString);
            command = new SqlCommand();
            command.Connection = connection;
        }


        /// <summary>
        /// register a new user
        /// </summary>
        public static void Register(string username, string password, string email)
        {
            lock (connection)
            {
                lock (command)
                {
                    connection.Open();
                    command.CommandText = "INSERT INTO users VALUES (@username,@password,@email,'','',@date)";
                    command.Parameters.AddWithValue("username", username);
                    command.Parameters.AddWithValue("password", password);
                    command.Parameters.AddWithValue("email", email);
                    command.Parameters.AddWithValue("date", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                    command.ExecuteNonQuery();
                    connection.Close();
                    command.Parameters.Clear();
                }
            }
        }


        /// <summary>
        /// login handling
        /// </summary>
        public static int Login(string username, string password)
        {
            lock (connection)
            {
                lock (command)
                {
                    connection.Open();
                    command.CommandText = "SELECT COUNT(*) FROM users WHERE username=@username AND password = @password";
                    command.Parameters.AddWithValue("username", username);
                    command.Parameters.AddWithValue("password", password);
                    int count = (int)command.ExecuteScalar();
                    connection.Close();
                    command.Parameters.Clear();
                    return count;
                }
            }
        }

        /// <summary>
        /// getting the user email
        /// </summary>
        public static string GetUserEmail(string username)
        {
            lock (connection)
            {
                lock (command)
                {
                    connection.Open();
                    command.CommandText = "SELECT email FROM users WHERE username=@username";
                    command.Parameters.AddWithValue("username", username);
                    string res = command.ExecuteScalar().ToString();
                    connection.Close();
                    command.Parameters.Clear();
                    return res;
                }
            }
        }


        /// <summary>
        ///   checking if username is available
        /// </summary>
        public static bool IsUserNameAvailable(string username)
        {
            lock (connection)
            {
                lock (command)
                {
                    connection.Open();
                    command.CommandText = "SELECT COUNT(*) FROM users WHERE username= @username";
                    command.Parameters.AddWithValue("username", username);
                    int count = (int)command.ExecuteScalar();
                    connection.Close();
                    command.Parameters.Clear();
                    if (count > 0)
                        return false;
                    return true;
                }
            }
        }

        /// <summary>
        ///   checking if a combination of username + email exists for password resets
        /// </summary>
        public static bool isExistUserWithEmail(string username, string email)
        {
            lock (connection)
            {
                lock (command)
                {
                    connection.Open();
                    command.CommandText = "SELECT COUNT(*) FROM users WHERE username= @username and email= @email";
                    command.Parameters.AddWithValue("username", username);
                    command.Parameters.AddWithValue("email", email);
                    int count = (int)command.ExecuteScalar();
                    connection.Close();
                    command.Parameters.Clear();
                    if (count > 0)
                        return true;
                    return false;
                }
            }
        }



        /// <summary>
        /// change a password
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public static void changePassword(string username, string password)
        {
            lock (connection)
            {
                lock (command)
                {
                    connection.Open();
                    command.CommandText = "UPDATE users SET password= @password,lastchangedpassword= @date  WHERE username= @username;";
                    command.Parameters.AddWithValue("username", username);
                    command.Parameters.AddWithValue("password", password);
                    command.Parameters.AddWithValue("date", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                    command.ExecuteNonQuery();
                    command.Parameters.Clear();
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// returns the last time the password was changed
        /// </summary>
        /// <param name="username"></param>
        public static DateTime getLastPasswordChange(string username)
        {
            lock (connection)
            {
                lock (command)
                {
                    connection.Open();
                    command.CommandText = "SELECT lastchangedpassword FROM users WHERE username= @username";
                    command.Parameters.AddWithValue("username", username);
                    string res = command.ExecuteScalar().ToString();
                    connection.Close();
                    command.Parameters.Clear();
                    return DateTime.ParseExact(res, "yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        /// <summary>
        ///   get all users the username followes
        /// </summary>
        public static string[] getUserFollowing(string username)
        {
            lock (connection)
            {
                lock (command)
                {
                    connection.Open();
                    command.CommandText = "SELECT following FROM users WHERE username= @username";
                    command.Parameters.AddWithValue("username", username);
                    string res = command.ExecuteScalar().ToString();
                    connection.Close();
                    command.Parameters.Clear();
                    return res.Split(',');
                }
            }
        }

        /// <summary>
        /// get all who follow a user
        /// </summary>
        public static string[] getUserFollowedBy(string username)
        {
            lock (connection)
            {
                lock (command)
                {
                    connection.Open();
                    command.CommandText = "SELECT followedby FROM users WHERE username= @username";
                    command.Parameters.AddWithValue("username", username);
                    string res = command.ExecuteScalar().ToString();
                    connection.Close();
                    command.Parameters.Clear();
                    return res.Split(',');
                }
            }
        }



        /// <summary>
        /// add follow
        /// </summary>
        public static void followUser(string requester, string target)
        {
            lock (connection)
            {
                lock (command)
                {
                    connection.Open();
                    command.CommandText = "SELECT following FROM users WHERE username= @requester";
                    command.Parameters.AddWithValue("requester", requester);
                    command.Parameters.AddWithValue("target", target);
                    string requesterFollowingList = command.ExecuteScalar().ToString();

                    string[] list = requesterFollowingList.Split(',');
                    bool already = false;
                    foreach (string i in list)
                    {
                        if (i.Equals(target)) already = true;
                    }
                    if (!already)
                    {
                        command.CommandText = "SELECT followedby FROM users WHERE username= @target";
                        string targetFollowedbyList = command.ExecuteScalar().ToString();

                        //updating requester
                        if (requesterFollowingList.Equals("")) { requesterFollowingList = target; }
                        else { requesterFollowingList += "," + target; }
                        command.CommandText = "UPDATE users SET following=@requesterFollowingList WHERE username= @requester;";
                        command.Parameters.AddWithValue("requesterFollowingList", requesterFollowingList);
                        command.ExecuteNonQuery();

                        //updating target
                        if (targetFollowedbyList.Equals("")) { targetFollowedbyList = requester; }
                        else { targetFollowedbyList += "," + requester; }
                        command.CommandText = "UPDATE users SET followedby= @targetFollowedbyList  WHERE username= @target;";
                        command.Parameters.AddWithValue("targetFollowedbyList", targetFollowedbyList);
                        command.ExecuteNonQuery();
                    }
                    command.Parameters.Clear();
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// unfollow a user
        /// </summary>
        public static void unFollowUser(string requester, string target)
        {
            lock (connection)
            {
                lock (command)
                {
                    connection.Open();
                    command.CommandText = "SELECT following FROM users WHERE username= @requester ";
                    command.Parameters.AddWithValue("requester", requester);
                    command.Parameters.AddWithValue("target", target);

                    string requesterFollowingList = command.ExecuteScalar().ToString();

                    string[] requesterList = requesterFollowingList.Split(',');
                    bool already = false;
                    foreach (string i in requesterList)
                    {
                        if (i.Equals(target)) already = true;
                    }
                    if (already)
                    {
                        command.CommandText = "SELECT followedby FROM users WHERE username= @target ";
                        string targetFollowedbyList = command.ExecuteScalar().ToString();
                        string[] targetList = targetFollowedbyList.Split(',');
                        //updating requester
                        string res;
                        if (requesterList.Length == 1)
                        {
                            if (requesterList[0].Equals(target))
                            {
                                res = "";
                            }
                            else
                            {
                                res = requesterList[0];
                            }
                        }
                        else
                        {
                            res = "";
                            foreach (string i in requesterList)
                            {
                                if (!i.Equals(target))
                                {
                                    res += i + ",";
                                }
                            }
                            res = res.Substring(0, res.Length - 1);
                        }
                        command.CommandText = "UPDATE users SET following=@reqres WHERE username=  @requester  ;";
                        command.Parameters.AddWithValue("reqres", res);

                        command.ExecuteNonQuery();

                        //updating target
                        if (targetList.Length == 1)
                        {
                            if (targetList[0].Equals(requester))
                            {
                                res = "";
                            }
                            else
                            {
                                res = targetList[0];
                            }
                        }
                        else
                        {
                            res = "";
                            foreach (string i in targetList)
                            {
                                if (!i.Equals(requester))
                                {
                                    res += i + ",";
                                }
                            }
                            res = res.Substring(0, res.Length - 1);
                        }
                        command.CommandText = "UPDATE users SET followedby=@tarres WHERE username= @target ;";
                        command.Parameters.AddWithValue("tarres", res);

                        command.ExecuteNonQuery();

                    }
                    command.Parameters.Clear();
                    connection.Close();
                }

            }
        }
    }

}
