using System;
using System.Text;

namespace VkAuctionConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;
            Console.WriteLine("VkBot Starting..");
            VkBot bot = new VkBot();
            bot.Start();

            while(Console.ReadKey().Key != ConsoleKey.Q) { }
        }
    }
}
