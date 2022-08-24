namespace VkAuctionConsole
{
    class AutorizationData
    {
        public ulong GroupId { get; set; }

        public string GroupToken { get; set; }

        /// <summary>
        /// Without message and audio restrictions
        /// </summary>
        public string UserToken { get; set; }
               
        public string SuperUserToken { get; set; }
    }
}
