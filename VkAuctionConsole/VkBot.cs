using System;
using VkNet;
using System.IO;
using System.Net;
using VkNet.Model;
using System.Linq;
using VkNet.Exception;
using System.Threading;
using System.Configuration;
using System.Globalization;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using VkNet.Enums.SafetyEnums;
using VkNet.Model.GroupUpdate;
using VkNet.Model.Attachments;
using VkNet.Model.RequestParams;
using System.Collections.Generic;
using VkNet.AudioBypassService.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace VkAuctionConsole
{
    class VkBot
    {
        public VkBot()
        {
            _groupApi = new VkApi();
            _superUserApi = new VkApi();
            _userApi = new VkApi();
            _autorizationData = GetAutorizationData();
            AutorizeAsUser();
            AutorizeAsSuperUser();
            AutorizeAsGroup();
        }

        private long _groupIdAsOwner;

        private AutorizationData _autorizationData;

        private readonly string _userToken;

        private static VkApi _groupApi;
        private static VkApi _superUserApi;
        private static VkApi _userApi;

        private AutoResetEvent are = new AutoResetEvent(true);
    
        public void Start()
        {
            Task.Run(() => LongPoll());
        }

        private void Autorize(string token, VkApi api)
        {
            try
            {
                api.Authorize(new ApiAuthParams { AccessToken = token });
            }
            catch(Exception ex)
            {
                
                Log($"{DateTime.Now} - {ex.Message}\nStack trace:\n{ex.StackTrace}");
            }
        }

        private AutorizationData GetAutorizationData()
        {
            return new AutorizationData
            {
                GroupId = ulong.Parse(ConfigurationManager.AppSettings["GroupId"]),
                GroupToken = ConfigurationManager.AppSettings["GroupToken"],
                UserToken = ConfigurationManager.AppSettings["UserToken"],
                SuperUserToken = ConfigurationManager.AppSettings["SuperUserToken"]
            };
        }

        private void AutorizeAsGroup() => Autorize(_autorizationData.GroupToken, _groupApi);
        
        private void AutorizeAsSuperUser() => Autorize(_autorizationData.SuperUserToken, _superUserApi);

        private void AutorizeAsUser() => Autorize(_autorizationData.UserToken, _userApi);

        private void LongPoll()
        {
            _groupIdAsOwner = (long)_autorizationData.GroupId * (-1);
            Console.WriteLine("Десериализация аукционов из файла..");
            string deserSuccMsg = AuctClass.DeserializeAuctions() 
                ? "Десериализация прошла успешно" 
                : "Ошибки при десериализации";
            Console.WriteLine(deserSuccMsg);
            Console.WriteLine($"{DateTime.Now} - VkBot started succefully!");
            AuctClass.CkeckAuctionsEndTime(_groupApi, _superUserApi, _groupIdAsOwner);

            BotsLongPollHistoryResponse poll = null;
            var srvr = _groupApi.Groups.GetLongPollServer(_autorizationData.GroupId);

            while (AuctClass.WorkFlag)
            {
                try
                {
                    poll = _groupApi.Groups.GetBotsLongPollHistory(new BotsLongPollHistoryParams
                    {
                        Server = srvr.Server,
                        Ts = srvr.Ts,
                        Key = srvr.Key,
                        Wait = 5
                    });

                    if (poll?.Updates == null || !poll.Updates.Any()) continue;
                    srvr.Ts = poll.Ts;
                }
                catch (LongPollKeyExpiredException) { }
                catch (Exception ex)
                {
                    Log($"{DateTime.Now} - When getting  poll: {ex.Message}\nStack trace:\n{ex.StackTrace}");
                    continue;
                }

                foreach (var upd in poll.Updates)
                {
                    try
                    {
                        if (upd.Type == GroupUpdateType.MessageNew)
                        {
                            AdminCmds(upd);
                        }

                        else if (upd.Type == GroupUpdateType.WallPostNew)
                        {
                            //AddAuction(upd.WallPost);
                        }

                        else if (upd.Type == GroupUpdateType.WallReplyNew)
                        {
                            if (((long)upd.WallReply.PostId).IsCommentToAuct() && upd.WallReply.Text.IsStavka() && upd.WallReply.ReplyToComment == null)
                            {
                                ProccessAuctComment(
                                    (long)upd.WallReply.PostId, 
                                    upd.WallReply.Id, 
                                    (long)upd.WallReply.FromId, 
                                    upd.WallReply.Text,
                                    CreateWallComment);
                            }
                        }

                        else if (upd.Type == GroupUpdateType.PhotoCommentNew)
                        {
                            if (((long)upd.PhotoComment.PhotoId).IsCommentToAuct() && upd.PhotoComment.Text.IsStavka() && upd.PhotoComment.ReplyToComment == null)
                            {
                                Console.WriteLine($"{DateTime.Now} -Photo comment: PhotoId- {upd.PhotoComment.PhotoId} PostId- {upd.PhotoComment.PostId} FromId- {upd.PhotoComment.FromId} Text- {upd.PhotoComment.Text ?? "null"}");
                                ProccessAuctComment(
                                    (long)upd.PhotoComment.PhotoId,
                                    upd.PhotoComment.Id,
                                    (long)upd.PhotoComment.FromId,
                                    upd.PhotoComment.Text,
                                    CreatePhotoComment);
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        Log($"{DateTime.Now} - In poll foreach: {ex.Message}\nStack trace:\n{ex.StackTrace}");
                    }
                }
            }
        }

        private void AddAuction(WallPost wp)
        {
            var wallText = wp.Text;
            if (wallText.IsAuction())
            {
                AuctClass.AddAuction(wallText, (long)wp.Id);
            }
            else if (wp.Attachments != null && wp.Attachments.Any())
            {
                foreach (var photo in wp.Attachments)
                {
                    try
                    {
                        var ph = (Photo)photo.Instance;
                        if (ph.Text.IsAuction())
                        {
                            AuctClass.AddAuction(ph.Text, (long)ph.Id, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"{DateTime.Now} - Exception on photo auct add: {ex.Message}\nStack trace:\n{ex.StackTrace}");
                    }

                }
            }
        }

        private void ProccessAuctComment(long postId, long commId, long fromId, string commText, Action<long, long, long, string, long, string> CreateComment)
        {
            Task.Run(() =>
            {
                string answer = AuctClass.ProccessBid(postId, commId, fromId, commText);

                if (answer == null) return;
                else if (answer.Contains("|"))
                {
                    try
                    {
                        var answSplited = answer.Split('|');
                        string info = answSplited[1];
                        long replyToCommId = long.Parse(answSplited[2]);
                        long repplyToUserId = long.Parse(answSplited[3]);
                        var user2 = GetUser(repplyToUserId);
                        string user2Name = $"[id{user2.Id}|{user2.FirstName}], " ?? " ";
                        CreateComment(_groupIdAsOwner, postId, replyToCommId, info.Insert(0, user2Name), 0, null);
                        //дублирование цены
                        //CreateComment(_groupIdAsOwner, postId, replyToCommId, answSplited[0].Remove(answSplited[0].Length - 2).Insert(0, user2Name));
                        answer = answSplited[0];
                    }
                    catch(Exception ex)
                    {
                        Log($"{DateTime.Now} - Exception on proccessBid: {ex.Message}\nStack trace:\n{ex.StackTrace}");
                    }
                }

                var user1 = GetUser(fromId);
                string user1Name = $"[id{user1.Id}|{user1.FirstName}], " ?? " ";

                CreateComment(_groupIdAsOwner, postId, commId, answer.Insert(0, user1Name), 0, null);
                
            });
        }

        private void AdminCmds(GroupUpdate upd)
        {
            Task.Run(() =>
            {
                if (AuctClass.Admins.Contains((long)upd.Message.FromId))
                {
                    string userMsg = upd.Message.Text;
                    string answer = AuctClass.ProccessMessagesCmd(userMsg);
                    if (answer != null)
                    {
                        try
                        {
                            //костыль из-за особенностей команды
                            if (answer == "addcmd")
                            {
                                var splitedMsg = userMsg.Split('_'); //это ссылка на пост и последний элемент это ид поста
                                //Autorize(_userToken, _groupApi);
                                var post = _userApi.Wall.GetById(new string[] { _groupIdAsOwner + "_" + splitedMsg[splitedMsg.Length - 1] });
                                //Autorize(_groupToken, _groupApi);

                                var wallPost = new WallPost
                                {
                                    Id = post.WallPosts[0].Id,
                                    Attachments = post.WallPosts[0].Attachments,
                                    Text = post.WallPosts[0].Text
                                };

                                AddAuction(wallPost);
                                answer = "Пост успешно добавлен!";
                            }

                            _groupApi.Messages.Send(new MessagesSendParams()
                            {
                                UserId = upd.Message.FromId,
                                Message = answer,
                                RandomId = new DateTime().Millisecond
                            });
                        }
                        catch(Exception ex)
                        {
                            Log($"{DateTime.Now} - Exception on proccess addCmd: {ex.Message}\nStack trace:\n{ex.StackTrace}");
                        }
                    }
                }
            });
        }

        private void CreateWallComment(long owId, long postId, long replToComm, string msg, long sid = 0, string cap = null)
        {
            try
            {
                _groupApi.Wall.CreateComment(new WallCreateCommentParams
                {
                    OwnerId = owId,
                    PostId = postId,
                    ReplyToComment = replToComm,
                    Message = msg
                });
            }
            catch(Exception ex)
            {
                Log($"{DateTime.Now} - Exception: {ex.Message}\nStack trace:\n{ex.StackTrace}");
            }
        }

        private string GetTokenWithoutMsgAndAudioRestrictions(ulong appId, string login, string password)
        {
            //Получение токена для обхода ограничений messages and audio
            var srvs = new ServiceCollection();
            srvs.AddAudioBypass();
            _groupApi = new VkApi(srvs);
            _groupApi.Authorize(new ApiAuthParams
            {
                ApplicationId = appId,
                Login = login,
                Password = password
            });
            return _groupApi.Token;
        }

        public static void CreatePhotoComment(long owId, long photoId, long replToComm, string msg, long sid = 0, string cap = null)
        {
            try
            {
                Thread.Sleep(1000);
                if(cap == null)
                {
                    _superUserApi.Photo.CreateComment(new PhotoCreateCommentParams
                    {
                        OwnerId = owId,
                        PhotoId = (ulong)photoId,
                        ReplyToComment = replToComm,
                        Message = msg
                    });
                }
                else
                {
                    _superUserApi.Photo.CreateComment(new PhotoCreateCommentParams
                    {
                        OwnerId = owId,
                        PhotoId = (ulong)photoId,
                        ReplyToComment = replToComm,
                        Message = msg,
                        CaptchaSid = sid,
                        CaptchaKey = cap
                    });
                }
            }
            catch(CaptchaNeededException ex)
            {
                string capPhoto = $"{ex.Sid}_captch.jpg";
                DowloadData(capPhoto, ImageFormat.Jpeg, ex.Img.AbsoluteUri);
                string capkey = TryGetCapFromRuCap(capPhoto);
                CreatePhotoComment(owId, photoId, replToComm, msg, (long)ex.Sid, capkey);
            }
            catch(Exception ex)
            {
                Log($"{DateTime.Now} - Exception on create photoComment: {ex.Message}\nStack trace:\n{ex.StackTrace}");
            }
        }

        private static string TryGetCapFromRuCap(string path)
        {
            Rucaptcha.Key = ConfigurationManager.AppSettings["RucaptchaKey"];
            var balance = Rucaptcha.Balance();
            double balanceD = Convert.ToDouble(double.Parse(balance, CultureInfo.InvariantCulture));
            Console.WriteLine($"{DateTime.Now} - Просит ввести капчу. Начинаю обработку капчи.. Balance: {balance}");
            if (balanceD < 0.1)
                return "Недостаточно средств на счете ruCaptcha";
            const string err = "ERROR";
            string capCode = err;
            while (capCode.Contains(err))
            {
                capCode = Rucaptcha.Recognize(path);
                string msg = capCode.Contains(err)
                ? $"Ошибка при получении капчи: {capCode}"
                : $"Капча получен: {capCode}";

                Console.WriteLine($"{DateTime.Now} - {msg}");
            }
            File.Delete(path);
            return capCode;
        }

        private static void DowloadData(string filename, ImageFormat format, string imageUrl)
        {
            using (WebClient webClient = new WebClient())
            {
                byte[] data = webClient.DownloadData(imageUrl);

                using (MemoryStream mem = new MemoryStream(data))
                {
                    using (var yourImage = System.Drawing.Image.FromStream(mem))
                    {
                        yourImage.Save(filename, format);
                    }
                }

            }
        }

        public static User GetUser(long id)
        {
            User user = null;
            try
            {
                user = _groupApi.Users.Get(new long[] { id })?.FirstOrDefault();
            }
            catch(Exception ex)
            {
                Log($"{DateTime.Now} - Exception on GetUser: {ex.Message}\nStack trace:\n{ex.StackTrace}");
            }
            return user;
        }


        private static readonly string LOG_FILE_NAME = "log.txt";
        private static object lockObj = new object();

        public static void Log(string msg)
        {
            lock (lockObj)
            {
                using (StreamWriter writer = new StreamWriter(LOG_FILE_NAME, true, System.Text.Encoding.Default))
                {
                    writer.WriteLine(msg);
                }
            }
        }
    }
}
