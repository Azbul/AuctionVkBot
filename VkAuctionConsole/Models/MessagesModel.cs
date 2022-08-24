using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace VkAuctionConsole.Models
{
    [DataContract]
    public class MessagesModel
    {
        [DataMember]
        public string Approve { get; set; }

        [DataMember]
        public string Wrong { get; set; }

        [DataMember]
        public string Perebita { get; set; }

        [DataMember]
        public string BidSmile { get; set; }

        [DataMember]
        public string Prodano { get; set; }
    }
}
