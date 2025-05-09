using Discord;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;

namespace DaineBot
{
    class Program
    {
        static void Main(string[] args) => new Bot().RunAsync().GetAwaiter().GetResult();
    }
}
