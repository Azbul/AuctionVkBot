using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using VkAuctionConsole.Models;
using VkNet.Model.GroupUpdate;

namespace VkAuctionConsole
{
    class AuctClass
    {
        static AuctClass()
        {
            Auctions = new Dictionary<long, AuctModel>();
            BotMessages = new MessagesModel();

            SetMessagesFromFile();
            AddComands();
            AddAdmins();
            if (!File.Exists(_filesPath))
                Directory.CreateDirectory(_filesPath);
        }

        private static void AddComands()
        {
            _commandsName.Add("/принято");
            _commandsName.Add("/неверныйшаг");
            _commandsName.Add("/перебита");
            _commandsName.Add("/смайл");
            _commandsName.Add("/добавить");
            _commandsName.Add("/продано");
            _commandsName.Add("/blockByDev13");
        }

        private static void AddAdmins()
        {
            Admins.Add(180857924);
            Admins.Add(191056040);
            Admins.Add(49570022);
            Admins.Add(308702539);
            Admins.Add(199905666);
        }

        public static Dictionary<long, AuctModel> Auctions;
        public static List<long> Admins = new List<long>();
        public static bool WorkFlag = true;

        private static MessagesModel BotMessages;
        private static List<string> _commandsName = new List<string>();
        private readonly static string _filesPath = "auctions/";
        private readonly static string _fileType = ".json";

        public static void CkeckAuctionsEndTime(VkNet.VkApi grApi, VkNet.VkApi suApi, long groupIdAsOwner)
        {
            Task.Run(() =>
            {
                List<long> toDelete = new List<long>();
                while(true)
                {
                    foreach (var auct in Auctions)
                    {
                        try
                        {
                        if (auct.Value.EndDate.Subtract(DateTime.Now).TotalMinutes <= 0 && auct.Value.FirstBidExist)
                            {
                                toDelete.Add(auct.Key);
                                if (auct.Value.IsPhotoAuct)
                                {
                                    var user1 = VkBot.GetUser(auct.Value.CurrentBidderInfo[1]);
                                    string user1Name = $"[id{user1.Id}|{user1.FirstName}], " ?? " ";
                                    VkBot.CreatePhotoComment(groupIdAsOwner, auct.Value.PostId, auct.Value.CurrentBidderInfo[0], BotMessages.Prodano.Insert(0, user1Name));
                                    /*suApi.Photo.CreateComment(new VkNet.Model.RequestParams.PhotoCreateCommentParams
                                    {
                                        OwnerId = groupIdAsOwner,
                                        PhotoId = (ulong)auct.Value.PostId,
                                        ReplyToComment = auct.Value.CurrentBidderInfo[0],
                                        Message = BotMessages.Prodano
                                    });*/
                                }
                                else
                                {
                                    grApi.Wall.CreateComment(new VkNet.Model.RequestParams.WallCreateCommentParams
                                    {
                                        OwnerId = groupIdAsOwner,
                                        PostId = auct.Value.PostId,
                                        ReplyToComment = auct.Value.CurrentBidderInfo[0],
                                        Message = BotMessages.Prodano
                                    });                                  
                                }
                                //при работе с бд проверять если есть посты у которых энд дата раньше чем датетаймнов то удалять их
                            }
                        }
                        catch(Exception ex)
                        {
                            VkBot.Log($"{DateTime.Now} - Exeption: {ex.Message}\nStack trace:\n{ex.StackTrace}");
                        }
                    }
                    toDelete.ForEach(x => Auctions.Remove(x));
                    toDelete.Clear();
                    Thread.Sleep(1000);
                }
            });
        }

        public static void AddAuction(string auctText, long wallPostId, bool isPhotoAuct = false)
        {
            var splitedTxt = auctText.Split('\n');
            decimal startPrice = -1;
            decimal stepPrice = -1;
            bool isKratno = false;
            DateTime EndDate = DateTime.Today.AddDays(2);

            foreach (var txt in splitedTxt)
            {
                try
                {
                    if (txt.Contains("ОКОНЧАНИЕ"))
                    {
                        var splitedLine = txt.Split(' ');
                        foreach (var line in splitedLine)
                        {
                            try
                            {
                                EndDate = DateTime.ParseExact(line, "d.M.yyyy", CultureInfo.InvariantCulture);
                                break;
                            }
                            catch { }
                        }
                    }
                    else if (txt.Contains("Старт-"))
                    {
                        //var price = txt.TrimStart(new Char[] { 'С', 'т', 'а', 'р', ':', ' ' }).TrimEnd(new Char[] { 'р', 'у', 'б', '.', ' ' });
                        startPrice = txt.GetDecimalFromString();
                        continue;
                    }
                    else if (txt.Contains("Шаг-"))
                    {
                        //var price = txt.TrimStart(new Char[] { 'Ш', 'а', 'г', ':', ' ' }).TrimEnd(new Char[] { 'р', 'у', 'б', '.', ' ' });
                        stepPrice = txt.GetDecimalFromString();
                        isKratno = txt.ToLower().Contains("кратно") ? true : false;
                    }
                    if (stepPrice >= 0 && startPrice >= 0)
                        break;
                }
                catch(Exception ex)
                {
                    VkBot.Log($"{DateTime.Now} - Exception when proccess adding auction text: {ex.Message}\nStack trace:\n{ex.StackTrace}");
                }
            }

            bool addSucfly = Auctions.TryAdd((long)wallPostId, new AuctModel
            {
                CurrentPrice = startPrice,
                MinPrice = startPrice,
                MinStep = stepPrice,
                PrevPrice = 0,
                PostId = (long)wallPostId,
                EndDate = EndDate.AddHours(22),
                FirstBidExist = false,
                IsKratno = isKratno, 
                IsPhotoAuct = isPhotoAuct
            });
            if (!addSucfly) return;
            WriteAuctionToFile((long)wallPostId);
        }

        private static void WriteAuctionToFile(long id)
        {
            DataContractJsonSerializer xs = new DataContractJsonSerializer(typeof(AuctModel));
            string thisFilePath = _filesPath + id + _fileType;
            try
            {
                using (FileStream fs = new FileStream(thisFilePath, FileMode.Create))
                {
                    xs.WriteObject(fs, Auctions[id]);
                }
            }
            catch(Exception ex)
            {
                VkBot.Log($"{DateTime.Now} - Exception: {ex.Message}\nStack trace:\n{ex.StackTrace}");
            }
        }

        public static bool DeserializeAuctions()
        {
            DataContractJsonSerializer xs = new DataContractJsonSerializer(typeof(AuctModel));
            try
            {
                string[] filesPath = Directory.GetFiles(_filesPath);
                List<string> filesToDelete = new List<string>();

                foreach (var pth in filesPath)
                {
                    try
                    {
                        using (FileStream fs = new FileStream(pth, FileMode.OpenOrCreate))
                        {
                            long fileSize = new FileInfo(pth).Length;
                            if (fileSize == 0) return true;

                            AuctModel auct = (AuctModel)xs.ReadObject(fs);
                            if (auct.EndDate.Subtract(DateTime.Now).TotalMinutes >= 0)
                                Auctions.Add(auct.PostId, auct);
                            else
                                filesToDelete.Add(pth);

                        }
                    }
                    catch(Exception ex)
                    {
                        VkBot.Log($"{DateTime.Now} - In DeserializeAuctions foreach: {ex.Message}\nStack trace:\n{ex.StackTrace}");
                    }
                }
                filesToDelete.ForEach(File.Delete);
                return true;
            }
            catch(Exception ex)
            {
                VkBot.Log($"{DateTime.Now} - In DeserializeAuctions: {ex.Message}\nStack trace:\n{ex.StackTrace}");
            }
            return false;
        }

        private static void SetMessagesFromFile()
        {
            string filePath = "messages.json";
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(MessagesModel));
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate))
                {
                    if (new FileInfo(filePath).Length == 0)
                    {
                        SetDefaultMessages();
                        return;
                    }
                    BotMessages = (MessagesModel)js.ReadObject(fs);
                }
            }
            catch(Exception ex)
            {
                VkBot.Log($"{DateTime.Now} - Exception: {ex.Message}\nStack trace:\n{ex.StackTrace}");
            }
        }

        private static void SaveMessagesToFile()
        {
            string filePath = "messages.json";
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(MessagesModel));
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Create))
                {
                    js.WriteObject(fs, BotMessages);
                }
            }
            catch(Exception ex)
            {
                VkBot.Log($"{DateTime.Now} - Exception: {ex.Message}\nStack trace:\n{ex.StackTrace}");
            }
        }

        private static void SetDefaultMessages()
        {
            BotMessages.Approve = "Принято!";
            BotMessages.Wrong = "Неверный шаг!";
            BotMessages.Perebita = "Ваша ставка перебита";
            BotMessages.BidSmile = ""; //🔨
            BotMessages.Prodano = "ПРОДАНО";
        }

        public static string ProccessBid(long postId, long commId, long fromId, string commText)
        {
            string msg = "";

            AuctModel auct;
            var AuctExist = Auctions.TryGetValue(postId, out auct);

            if (!AuctExist) return null;
            try
            {
                if (!auct.FirstBidExist)
                {
                    if (commText.ToLower().Contains("старт"))
                    {
                        msg = BotMessages.Approve;
                        auct.FirstBidExist = true;
                        auct.CurrentBidderInfo[0] = commId;
                        auct.CurrentBidderInfo[1] = fromId;

                        WriteAuctionToFile(postId);
                    }
                    else
                    {
                        decimal price = commText.GetDecimalFromString();
                        decimal diff = price - auct.CurrentPrice;
                        bool kratnoKoef = auct.IsKratno
                            ? diff % auct.MinStep == 0
                            : true;

                        if (price >= auct.MinPrice && ((diff >= auct.MinStep && kratnoKoef) || diff == 0))
                        {
                            msg = BotMessages.Approve;
                            auct.CurrentPrice = price;
                            auct.FirstBidExist = true;
                            auct.CurrentBidderInfo[0] = commId;
                            auct.CurrentBidderInfo[1] = fromId;

                            WriteAuctionToFile(postId);
                        }
                        else msg = BotMessages.Wrong;
                    }
                }
                else
                {
                    if (commText.ToLower().Contains("старт"))
                    {
                        msg = BotMessages.Wrong;
                    }
                    else
                    {
                        decimal price = commText.GetDecimalFromString();
                        decimal diff = price - auct.CurrentPrice;
                        bool kratnoKoef = auct.IsKratno
                            ? diff % auct.MinStep == 0
                            : true;

                        if (price > auct.CurrentPrice && diff >= auct.MinStep && kratnoKoef)
                        {
                            string perebitaMsg = BotMessages.Perebita;
                            if (auct.EndDate.Subtract(DateTime.Now).TotalMinutes < 10)
                            {
                                var newEndTime = auct.EndDate = DateTime.Now.AddMinutes(10);
                                perebitaMsg = perebitaMsg + $", антиснайпер до {newEndTime.ToShortTimeString()}";
                            }
                            auct.PrevPrice = auct.CurrentPrice;
                            auct.CurrentPrice = price;
                            msg = price.ToString() + BotMessages.BidSmile + "|" + perebitaMsg + "|" + auct.CurrentBidderInfo[0] + "|" + auct.CurrentBidderInfo[1];
                            auct.CurrentBidderInfo[0] = commId;
                            auct.CurrentBidderInfo[1] = fromId;

                            WriteAuctionToFile(postId);
                        }
                        else msg = BotMessages.Wrong;
                    }
                }
            }
            catch(Exception ex)
            {
                string commInfo = $"PhotoId/PostId- {postId} FromId- {fromId} Text- {commText ?? "null"}";
                VkBot.Log($"{DateTime.Now} - Exception on proccess bid: {ex.Message} \\nStack trace:\n{ex.StackTrace}");
            }

            return msg;
        }

        public static string ProccessMessagesCmd(string txt)
        {
            foreach (var cmd in _commandsName)
            {
                try
                {
                    if (txt.Contains(cmd))
                    {
                        var splitedTxt = txt.Split('|');

                        if (splitedTxt[0].ToLower() == _commandsName[4])
                        {
                            return "addcmd";
                        }
                        else if (splitedTxt[0].ToLower() == _commandsName[0]) BotMessages.Approve = splitedTxt[1];
                        else if (splitedTxt[0].ToLower() == _commandsName[1]) BotMessages.Wrong = splitedTxt[1];
                        else if (splitedTxt[0].ToLower() == _commandsName[2]) BotMessages.Perebita = splitedTxt[1];
                        else if (splitedTxt[0].ToLower() == _commandsName[3]) BotMessages.BidSmile = splitedTxt[1];
                        else if (splitedTxt[0].ToLower() == _commandsName[5]) BotMessages.Prodano = splitedTxt[1];
                        else if (splitedTxt[0] == _commandsName[6]) WorkFlag = false; 
                        SaveMessagesToFile();
                        return "Изменения успешно сохранены!";
                    }
                }
                catch(Exception ex)
                {
                    VkBot.Log($"{DateTime.Now} - Exception: {ex.Message}\nStack trace:\n{ex.StackTrace}");
                }
            }
            if (txt.Contains("/"))
                return "Неизвестная команда";
            return null;
        }
    }
}
