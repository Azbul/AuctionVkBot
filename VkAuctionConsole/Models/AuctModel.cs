using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace VkAuctionConsole
{
    [DataContract]
    public class AuctModel
    {
        [DataMember]
        public long PostId { get; set; }

        [DataMember]
        public long[] CurrentBidderInfo { get; set; } = new long[2];

        [DataMember]
        public decimal CurrentPrice { get; set; }

        [DataMember]
        public decimal PrevPrice { get; set; }

        [DataMember]
        public decimal MinPrice { get; set; }

        [DataMember]
        public decimal MinStep { get; set; }

        [DataMember]
        public DateTime EndDate { get; set; }

        [DataMember]
        public bool FirstBidExist { get; set; }

        [DataMember]
        public bool IsKratno { get; set; }

        [DataMember]
        public bool IsPhotoAuct { get; set; }
    }
}
