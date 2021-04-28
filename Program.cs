using DSharpPlus;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using tarsbot.events;

namespace tarsbot
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            string token = File.ReadAllText(@"token.txt");

            DiscordClient client = new DiscordClient(new DiscordConfiguration()
            {
                Token = token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.AllUnprivileged
            });

            client.MessageCreated += MessageListener.MessageCreated;
            client.Ready += OnReady;
            

            await client.ConnectAsync();
            await Task.Delay(-1);
        }

        static async Task OnReady(DiscordClient sender, ReadyEventArgs args)
        {
            
        }
    }
}
