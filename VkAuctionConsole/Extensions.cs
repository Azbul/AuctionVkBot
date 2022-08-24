using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VkNet.Model.GroupUpdate;

namespace VkAuctionConsole
{
    static class Extensions
    {
        public static bool IsAuction(this string txt)
        {
            return !string.IsNullOrEmpty(txt)
                && txt.Contains("ОКОНЧАНИЕ")
                && txt.Contains("Старт-")
                && txt.Contains("Шаг-");
        }

        public static bool IsStavka(this string txt)
        {
            return !string.IsNullOrEmpty(txt) 
                && txt.Length < 41 
                && (txt.Replace(" ", "").All(Char.IsDigit) || txt.ToLower().Contains("старт"));
        }

        public static decimal GetDecimalFromString(this string txt)
        {
            return Convert.ToDecimal(new String(txt.Where(Char.IsDigit).ToArray()));
        }

        public static bool IsCommentToAuct(this long postId)
        {
            return AuctClass.Auctions.ContainsKey(postId);
        }
    }
}
