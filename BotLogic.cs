using Azure;
using Azure.AI.OpenAI;
using Discord;
using Discord.WebSocket;
using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ChatGPT_Discord_Bot
{
    public class BotLogic
    {
        private DiscordSocketClient _client;
        private string _discordToken;
        private string _openAIKey;
        private OpenAIClient _openAI;
        private string _adminID; //my discord user id 

        public BotLogic()
        {
            _client = new DiscordSocketClient();
            _discordToken =  GoogleSecrets.GetDiscordToken().Result;
            _openAIKey = GoogleSecrets.GetOpenAIKey().Result;
            _openAI = new OpenAIClient(_openAIKey);
            _adminID = GoogleSecrets.GetAdminID().Result;
        }

        public async Task Start()
        {
            _client.Log += LogAsync;
            _client.MessageReceived += MessageReceivedAsync;
            _client.Ready += Client_Ready;
            _client.SlashCommandExecuted += SlashCommandHandler;

            await _client.LoginAsync(TokenType.Bot, _discordToken);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task Client_Ready()
        {
            Console.WriteLine("Bot is connected");

            var banCommand = new SlashCommandBuilder()
                .WithName("ban")
                .WithDescription("ban user")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("user")
                    .WithDescription("user to ban")
                    .WithType(ApplicationCommandOptionType.User)
                    .WithRequired(true));

            var unbanCommand = new SlashCommandBuilder()
                .WithName("unban")
                .WithDescription("unban user")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("user")
                    .WithDescription("user to unban")
                    .WithType(ApplicationCommandOptionType.User)
                    .WithRequired(true));

            var unlockChannelCommand = new SlashCommandBuilder()
                .WithName("unlock")
                .WithDescription("unlock channel");

            var lockChannelCommand = new SlashCommandBuilder()
                .WithName("lock")
                .WithDescription("lock channel");

            var unlockServerCommand = new SlashCommandBuilder()
                .WithName("unlockserver")
                .WithDescription("unlock server");

            var lockServerCommand = new SlashCommandBuilder()
                .WithName("lockserver")
                .WithDescription("lock server");

            var bugreportCommand = new SlashCommandBuilder()
                .WithName("reportbug")
                .WithDescription("report a bug")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("description")
                    .WithDescription("description of the issue found")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true));

            await _client.CreateGlobalApplicationCommandAsync(banCommand.Build());
            await _client.CreateGlobalApplicationCommandAsync(unbanCommand.Build());
            await _client.CreateGlobalApplicationCommandAsync(unlockChannelCommand.Build());
            await _client.CreateGlobalApplicationCommandAsync(lockChannelCommand.Build());
            await _client.CreateGlobalApplicationCommandAsync(unlockServerCommand.Build());
            await _client.CreateGlobalApplicationCommandAsync(lockServerCommand.Build());
            await _client.CreateGlobalApplicationCommandAsync(bugreportCommand.Build());

        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(SocketMessage message)
        {

            Console.WriteLine("User: {0} Said: \" {1} \", in: {2}", message.Author.ToString(), message.Content.ToString(), message.Channel.ToString());

            if (message.Author.IsBot) return;

            if (await DbStorage.CheckBanUser(message.Author.Id.ToString()))
            {
                return;
            }

            if (await DbStorage.CheckChannel(message.Channel.Id.ToString()))
            {
                return;
            }

            if (message.MentionedUsers.Any(user => user.Id == _client.CurrentUser.Id) || message.Channel.GetType() == typeof(SocketDMChannel))
            {
                var userMessage = message.Content.Replace($"<@!{_client.CurrentUser.Id}>", "").Trim();

                string response;

                if (message.Content.Contains("!F"))
                {
                    response = await GetOpenAIResponseNoFormatt(userMessage);
                }
                else
                {
                    await DbStorage.AddMessage(message.Channel.Id.ToString(), message.Author.Username, userMessage);

                    response = await GetOpenAIResponse(message.Channel.Id.ToString());
                }

                const int discordMessageLimit = 2000; // Discord message char limit
                if (response.Length > discordMessageLimit)
                {
                    var messages = SplitMessage(response, discordMessageLimit);
                    foreach (var msg in messages)
                    {
                        await message.Channel.SendMessageAsync(msg);
                    }
                }
                else
                {
                    await message.Channel.SendMessageAsync(response);
                }
            }

            await DbStorage.RemoveOldMessages(message.Channel.Id.ToString());
        }


        private async Task<string> GetOpenAIResponse(string channelID)
        {
            var chatCompletionsOptions = new ChatCompletionsOptions()
            {
                DeploymentName = "gpt-4o", // Use DeploymentName for "model" with non-Azure clients
                                           // The system message represents instructions or other guidance about how the assistant should behave

                Messages =
                {
                    new ChatRequestSystemMessage(await GoogleSecrets.GetPrompt())
                }
            };

            foreach (string message in await DbStorage.GetMessages(channelID))
            {
                chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(message));
            }

            Response<ChatCompletions> response = await _openAI.GetChatCompletionsAsync(chatCompletionsOptions);
            ChatResponseMessage responseMessage = response.Value.Choices[0].Message;
            return responseMessage.Content;
        }

        private async Task<string> GetOpenAIResponseNoFormatt(string userMessage)
        {
            var chatCompletionsOptions = new ChatCompletionsOptions()
            {
                DeploymentName = "gpt-4o", // Use DeploymentName for "model" with non-Azure clients
                                           // The system message represents instructions or other guidance about how the assistant should behave

                Messages =
                {
                    new ChatRequestUserMessage(userMessage)
                }
            };

            Response<ChatCompletions> response = await _openAI.GetChatCompletionsAsync(chatCompletionsOptions);
            ChatResponseMessage responseMessage = response.Value.Choices[0].Message;
            return responseMessage.Content;
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            switch (command.Data.Name)
            {
                case "ban":
                    if (command.User.Id.ToString() == _adminID)
                    {
                        await DbStorage.BanUser(command.Data.Options.First().Value.ToString());
                        await command.RespondAsync("User has been banned");
                    }
                    else
                    {
                        await command.RespondAsync("You do not have permission to use this command");
                    }
                    break;
                case "unban":
                    if (command.User.Id.ToString() == _adminID)
                    {
                        await DbStorage.UnbanUser(command.Data.Options.First().Value.ToString());
                        await command.RespondAsync("User has been unbanned");
                    }
                    else
                    {
                        await command.RespondAsync("You do not have permission to use this command");
                    }
                    break;
                case "unlock":
                    if (command.User.Id.ToString() == _adminID)
                    {
                        await DbStorage.AddChannel(command.Channel.Id.ToString());
                        await command.RespondAsync("Channel has been unlocked");
                    }
                    else
                    {
                        await command.RespondAsync("You do not have permission to use this command");
                    }
                    break;
                case "lock":
                    if (command.User.Id.ToString() == _adminID)
                    {
                        await DbStorage.RemoveChannel(command.Channel.Id.ToString());
                        await command.RespondAsync("Channel has been locked");
                    }
                    else
                    {
                        await command.RespondAsync("You do not have permission to use this command");
                    }
                    break;
                case "bugreport":
                    string bugDescription = command.Data.Options.First().Value.ToString();
                    await sendManual($"Bug report: {bugDescription}", "1243585805180735558");
                    await command.RespondAsync("Bug report has been sent");
                    break;
            }
      
        }

        public async Task sendManual(string message, string channelID)
        {
            foreach(var guild in _client.Guilds)
            {
                if (guild.GetTextChannel(ulong.Parse(channelID)) != null)
                {
                    await guild.GetTextChannel(ulong.Parse(channelID)).SendMessageAsync(message);
                }
            }
        }

        public async Task sendDM(string message, string userID)
        {
            var user = _client.GetUser(ulong.Parse(userID));
            await user.SendMessageAsync(message);
        }

        private static IEnumerable<string> SplitMessage(string message, int chunkSize)
        {
            for (int i = 0; i < message.Length; i += chunkSize)
            {
                yield return message.Substring(i, Math.Min(chunkSize, message.Length - i));
            }
        }

    }
}
