using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TwitterServer
{
    /// <summary>
    /// manages all login and account related stuff
    /// </summary>
    static class LoginUtils
    {
        private const string emailAddress = "smtpjii7@gmail.com";
        private const string emailPassword = "fkfd yrbi nakb bmoy";
        private static SHA256Managed sha256=new SHA256Managed();
        private static Random random = new Random();


        //allows you to disable captcha checks while testing (If you make this false you can just click continue on the client)
        private const bool doEmailCaptchaCheck = true;

        //the maximum auth fails allows
        private const int maxAuthFails= 5;

        /// <summary>
        /// forcing to change password after certain period
        /// </summary>
        private const int DaysToChangePassword = 30;

        //to reserve names between login and auth
        private static Dictionary<UserCommunication,string> reservedNames = new Dictionary<UserCommunication,string>();

        /// <summary>
        /// login function
        /// </summary>
        public static void login(UserCommunication user, string output)
        {
            JsonElement root = JsonDocument.Parse(output.Substring(1)).RootElement;
            string username = root.GetProperty("username").ToString();
            string password = root.GetProperty("password").ToString();
            int res = UsersSqlHandler.Login(username, GetSha256(password));

            if (res == 0)
            {
                Logging.Log(user.GetIp() + " entered wrong login credentials, closing connection ");
                user.WriteBytes(Encoding.UTF8.GetBytes( "1"));
                user.Close();

            }
            else
            {
                Logging.Log(user.GetIp() + " entered correct login credentials for user " + username + " ,sent code for email auth to " + UsersSqlHandler.GetUserEmail(username));

                if (authenticaton(user, username, UsersSqlHandler.GetUserEmail(username)))
                {
                    bool logged = true;
                    //good auth
                    if (DateTime.Now.Subtract(UsersSqlHandler.getLastPasswordChange(username)).Days > DaysToChangePassword)
                    {
                        //must replace password
                        Logging.Log(user.GetIp() + " as " + username + " must change he's password because it is expired");
                        user.WriteBytes(Encoding.UTF8.GetBytes("3"));
                        logged = false;
                        while (!logged)
                        {
                            try
                            {
                                string newPassword = Encoding.UTF8.GetString(user.ReadBytes());
                                if (checkPassword(newPassword) && (!password.Equals(newPassword)))
                                {
                                    logged = true;
                                    UsersSqlHandler.changePassword(username, newPassword);
                                }
                                else
                                {
                                    user.WriteBytes(Encoding.UTF8.GetBytes("1"));
                                }
                            }
                            catch
                            {//disconnect
                                break;
                            }
                        }
                    }
                    if (logged)
                    {
                        //logged in-> success
                        user.WriteBytes(Encoding.UTF8.GetBytes("0"));
                        TwitterServerMain.connectedUsers.Add(username);
                        user.StartReading(username);
                        Logging.Log(user.GetIp() + " logged into user " + username);
                    }
                }
                else {
                    Logging.Log(user.GetIp() + " failed auth to "+username);
                }

            }
        }

        /// <summary>
        /// register function
        /// </summary>
        public static void register(UserCommunication user, string output)
        {
            JsonElement root = JsonDocument.Parse(output.Substring(1)).RootElement;
            string username = root.GetProperty("username").ToString();
            string password = root.GetProperty("password").ToString();
            string email = root.GetProperty("email").ToString();
            byte[] result = new byte[1]; ;

            if (username.Contains('\'') || email.Contains('\'') || username.Contains(',') || email.Contains(','))
            {
                //illegal charcters check
                result = Encoding.UTF8.GetBytes("20");
                Logging.Log(user.GetIp() + " failed to register because of illegal characters");
            }
            else if (username.Length < 1)
            {
                //illegal charcters check
                result = Encoding.UTF8.GetBytes("22");
                Logging.Log(user.GetIp() + " failed to register because username was too short");
            }
            else if (!checkPassword(password)) {
                //invalid password
                result = Encoding.UTF8.GetBytes("23");
                Logging.Log(user.GetIp() + " failed to register because password was either too short or needed more variety");
            }
            else if (email.Split('@').Length != 2)
            {
                //invalid email
                result = Encoding.UTF8.GetBytes("21");
                Logging.Log(user.GetIp() + " failed to register because of invalid email address");
            }
            else if (email.Split('@')[1].Split('.').Length <= 1)
            {
                //invalid email
                result = Encoding.UTF8.GetBytes("21");
                Logging.Log(user.GetIp() + " failed to register because of invalid email address");
            }
            else if (email.Split('@')[1].Split('.')[1].Length == 0 || email.Split('@')[1].Split('.')[0].Length == 0)
            {
                //invalid email
                result = Encoding.UTF8.GetBytes("21");
                Logging.Log(user.GetIp() + " failed to register because of invalid email address");
            }
            else
            {


                bool isOk = UsersSqlHandler.IsUserNameAvailable(username) && (!reservedNames.Values.Contains(username));
                if (isOk)
                {   //user name is ok, now start the auth process
                    reservedNames.Add(user, username);

                    if (authenticaton(user, username, email))
                    {
                        UsersSqlHandler.Register(username, GetSha256(password), email);
                        result = Encoding.UTF8.GetBytes("0");
                        Logging.Log(user.GetIp() + " registered user " + username);
                    }
                    else
                    {
                        Logging.Log(user.GetIp() + " failed auth");
                    }
                    reservedNames.Remove(user);
                }
                else
                {
                    result = Encoding.UTF8.GetBytes("1");
                    Logging.Log(user.GetIp() + " failed to register user " + username + " because it was taken");

                }
            }
            try
            {
                user.WriteBytes(result);
            }
            catch {/*in case of disconnect*/ }
            user.Close();
        }

        /// <summary>
        /// reset password
        /// </summary>
        /// <param name="user"></param>
        /// <param name="output"></param>
        public static void ResetPassword(UserCommunication user, string username,string email,string newPass) {
            byte[] result= Encoding.UTF8.GetBytes("0");
            if (!UsersSqlHandler.isExistUserWithEmail(username, email))
            {
                result = Encoding.UTF8.GetBytes("1wrong username or email");
                Logging.Log(user.GetIp() + " failed to reset password because email or username were wrong");

            }
            else if (!checkPassword(newPass)) {
                result = Encoding.UTF8.GetBytes("1Password at least 8 chars long,\n and include lower,upper,number,special\n type letters");
                Logging.Log(user.GetIp() + " failed to reset password because it was either too short or needed more variety");
            }
            else
            {

                if (authenticaton(user, username, email))
                {
                    UsersSqlHandler.changePassword(username, GetSha256(newPass));
                    result = Encoding.UTF8.GetBytes("0");
                    Logging.Log(user.GetIp() + " as " + username + " changed password");
                }
                else
                {
                    result = Encoding.UTF8.GetBytes("1auth failed");
                    Logging.Log(user.GetIp() + " failed auth");
                }
            }
            user.WriteBytes(result);
            user.Close();
        }


        /// <summary>
        /// responsible for authentication in login and register phase using email and captcha
        /// </summary>
        /// <param name="user"></param>
        /// <param name="username"></param>
        /// <param name="email"></param>
        /// <returns></returns>
        private static bool authenticaton(UserCommunication user,string username,string email) {
            string code = random.Next(1000000).ToString();
            code = new string('0', (6 - code.Length)) + code;
            byte[] data = Encoding.UTF8.GetBytes(("0" + email).ToString());
            user.WriteBytes(data);

            string captchaTxt = CaptchaUtils.GenerateCaptchaText(6);
            user.WriteBytes(CaptchaUtils.GenerateCapchaImage(captchaTxt));

            if (doEmailCaptchaCheck)
            {
                SendEmail(emailAddress, email, emailPassword, "Your verification code", "Your code is " + code);
            }

            DateTime initial= DateTime.Now;

            int fails = 0;
            string ans;

            //so we can check time limit and input in the same time
            user.GetTcpClient().ReceiveTimeout = 15000;

            while (maxAuthFails>fails)
            {
                if (DateTime.Now.Subtract(initial).Minutes  >= 10)
                {
                    //time limit exceded
                    user.WriteBytes(Encoding.UTF8.GetBytes("2"));
                    break;
                }
                try
                {
                    ans = Encoding.UTF8.GetString(user.ReadBytes());
                }
                catch{
                    if (!user.GetTcpClient().Connected)
                        break;
                    else {
                        continue;
                    }
                }
                string userCode = ans.Split(':')[0];
                string userCaptchaCode = ans.Split(':')[1];
                if ((userCode.Equals(code) && userCaptchaCode.Equals(captchaTxt)) || (!doEmailCaptchaCheck))
                {
                    //right code=login
                    user.GetTcpClient().ReceiveTimeout = 0;
                    return true;
                }
                else
                {
                    fails++;
                    if (maxAuthFails > fails)
                    {
                        user.WriteBytes(Encoding.UTF8.GetBytes("1"));
                        Logging.Log(user.GetIp() + " entered wrong email or captcha code for user " + username);
                    }
                    else {
                        //failed more than the maximum amount
                        string ip = user.GetTcpClient().Client.RemoteEndPoint.ToString().Split(':')[0];
                        TwitterServerMain.IPSCORE[ip] += 5;
                        if (TwitterServerMain.IPSCORE[ip] >= TwitterServerMain.banPoint)
                        {
                            TwitterServerMain.blockedIps.Add(ip);
                        }
                        user.WriteBytes(Encoding.UTF8.GetBytes("2"));
                    }
                }
            }
            try
            {
                user.GetTcpClient().ReceiveTimeout = 0;
            }
            catch {//in case of disconnect
                   }
            return false;
        }

        /// <summary>
        /// checking if a password meets all standards
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        static bool checkPassword(string password) {
            if (!password.Any(char.IsUpper))
                return false;
            if (!password.Any(char.IsLower))
                return false;
            if (!password.Any(char.IsDigit))
                return false;
            if (!password.Any(ch => !char.IsLetterOrDigit(ch))) {
                return false;
            }
            if (password.Length < 8)
                return false;
            return true;
               
        }

        /// <summary>
        /// send email
        /// </summary>
        static void SendEmail(string myEmailAddress, string toEmailAddress, string emailPassword, string title, string content)
        {

            MailMessage mailMessage = new MailMessage(myEmailAddress, toEmailAddress, title, content);
            SmtpClient smtpClient = new SmtpClient("smtp.gmail.com", 587);
            smtpClient.EnableSsl = true;
            smtpClient.Credentials = new NetworkCredential(myEmailAddress, emailPassword);
            smtpClient.Send(mailMessage);

        }
        
        /// <summary>
        /// get sha256 hash of a string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        static string GetSha256(string input)
        {
            byte[] data = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            string res = "";
            foreach (byte theByte in data)
            {
                res += theByte.ToString("x2");
            }
            return res;
        }
    }
}
