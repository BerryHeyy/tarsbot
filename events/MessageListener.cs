using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.EventArgs;

using tarsbot.commands;

namespace tarsbot.events
{
    class MessageListener
    {
        public static async Task MessageCreated(DiscordClient client, MessageCreateEventArgs ctx)
        {
            if (ctx.Author.IsBot) return;

            string message = ctx.Message.Content;

            if (!message.StartsWith("$")) return;

            message = message.Substring(1);
            bool dumpMemory = false;
            bool dumpFull = false;

            if (message.Contains('-'))
            {
                dumpMemory = message.Contains("--dumpMemory");
                dumpFull = message.Contains("--dumpMemoryFull");

                message = message.Substring(0, message.IndexOf('-'));
                message = message.Substring(0, message.Length - 1);
            }

            ProgramEnvironment environment = new ProgramEnvironment(message, ctx.Channel);

            if (await environment.Compile(dumpMemory, dumpFull)) await environment.Run(dumpMemory, dumpFull);
        }
    }
}
