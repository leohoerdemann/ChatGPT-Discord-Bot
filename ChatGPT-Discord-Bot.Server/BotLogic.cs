﻿using Discord;
using Discord.Audio;
using Discord.WebSocket;
using OpenAI;
using OpenAI.Chat;
using static Google.Cloud.Firestore.V1.StructuredAggregationQuery.Types.Aggregation.Types;

namespace ChatGPT_Discord_Bot.Server
{
    public class BotLogic
    {
        public DiscordSocketClient _client { get; private set; }
        public OpenAIClient _openAIClient { get; private set; }

        //private readonly DbStorage _dbStorage;
        private string _authorizedUserId;
        private const int DiscordMessageLimit = 2000;

        private string initialPrompt;

        public DbStorage DbStorage { get; private set; } = new DbStorage();

        private Dictionary<string , DateTime> _timeOuts = new Dictionary<string, DateTime>();

        private Dictionary<string, int> _userLimits = new Dictionary<string, int>();

        public int MessageHistoryLimit = 3;


        public BotLogic()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
                UseInteractionSnowflakeDate = false
            });

            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.MessageReceived += MessageReceivedHandler;
            _client.SlashCommandExecuted += SlashCommandHandler;

            initialPrompt = GoogleSecrets.GetPrompt().Result;
        }

        public void UpdatePrompt()
        {
            initialPrompt = GoogleSecrets.GetPrompt().Result;
        }

        public async Task SetStatus(string status)
        {
            await _client.SetActivityAsync(new Game(status));
        }

        private async Task MessageReceivedHandler(SocketMessage arg)
        {

            if (arg.Channel.GetChannelType() == ChannelType.DM)
            {
                Console.WriteLine($"{arg.Author} in DM: {arg.Content}");
            }
            else
            {
                Console.WriteLine($"{arg.Author} in {(arg.Channel as SocketGuildChannel)?.Guild.Name},{arg.Channel}: {arg.Content}");
            }

            // Check if the message is from a user and not a bot
            if (arg.Author.IsBot) return;

            // Check if the message starts with a mention of the bot
            if (arg.MentionedUsers.Any(user => user.Id == _client.CurrentUser.Id))
            {


                DbStorage.updateStats(arg.Channel.Name.ToString(), arg.Author.ToString());

                // Check if the user is timed out
                if (_timeOuts.TryGetValue(arg.Author.Id.ToString(), out DateTime timeout))
                {
                    if (DateTime.Now < timeout)
                    {
                        await arg.Channel.SendMessageAsync("Sorry you are currently timed out");
                        return;
                    }
                    else
                    {
                        _timeOuts.Remove(arg.Author.Id.ToString());
                    }
                }

                // Check if the user has a message limit
                if (_userLimits.TryGetValue(arg.Author.Id.ToString(), out int limit))
                {
                    if (limit <= 0)
                    {
                        await arg.Channel.SendMessageAsync("Sorry you have reached your message limit");
                        return;
                    }
                    else
                    {
                        _userLimits[arg.Author.Id.ToString()] = limit - 1;
                    }
                }

                // Prepare the message history
                var messages = new List<ChatMessage>();

                // Optionally, you can add system prompts or previous context
                if (!string.IsNullOrEmpty(initialPrompt))
                {
                    messages.Add(new SystemChatMessage(initialPrompt));
                }

                var channel = arg.Channel as SocketGuildChannel;

                if (arg.Channel.GetChannelType() == ChannelType.DM)
                {
                    messages.Add(new SystemChatMessage($"You are currently in a DM with the {arg.Author.Username.ToString()}"));
                }
                else
                {
                    messages.Add(new SystemChatMessage($"You are currently in the {channel.Name} channel in the {channel.Guild.Name} server"));
                }

                var last20messages = await arg.Channel.GetMessagesAsync(MessageHistoryLimit).FlattenAsync();

                foreach (var message in last20messages)
                {
                    if (message.Author.IsBot)
                    {
                        messages.Add(new AssistantChatMessage($"({message.Timestamp.ToString()}) {message.Content}"));
                    }
                    else
                    {
                        var question = message.Content.Replace($"<@{_client.CurrentUser.Id}>", "").Trim();

                        messages.Add(new UserChatMessage($"{message.Author.Username} at {message.Timestamp.ToString()} said: {question}"));
                    }
                }

                var chat = _openAIClient.GetChatClient("gpt-4o");

                // Send the messages to ChatGPT
                var result = chat.CompleteChatAsync(messages);
                var chatResponse = new AssistantChatMessage(result.Result);

                if (chatResponse != null && chatResponse.Content.Count > 0)
                {
                    var responseText = chatResponse.Content[0].Text;

                    // Split response into chunks if necessary
                    var responseChunks = SplitMessage(responseText);

                    // Send response chunks
                    foreach (var chunk in responseChunks)
                    {
                        await arg.Channel.SendMessageAsync(chunk);
                    }
                }
                else
                {
                    await arg.Channel.SendMessageAsync("I'm sorry, I couldn't generate a response.");
                }
            }
        }

        public async Task InitializeAsync()
        {
            var discordToken = await GoogleSecrets.GetDiscordToken();
            await _client.LoginAsync(TokenType.Bot, discordToken);
            await _client.StartAsync();
            await _client.SetActivityAsync(new Game("Bot Stuff"));

            var openAiApiKey = await GoogleSecrets.GetOpenAIKey();

            // Instantiate the OpenAIClient
            _openAIClient = new OpenAIClient(openAiApiKey);

            _authorizedUserId = await GoogleSecrets.GetAdminID();
        }

        private Task LogAsync(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task ReadyAsync()
        { 
            // Register new commands
            var commands = new List<ApplicationCommandProperties>
                    {
                    new SlashCommandBuilder()
                        .WithName("ask")
                        .WithDescription("Ask a question to ChatGPT 4o with context")
                        .AddOption("question", ApplicationCommandOptionType.String, "Your question", isRequired: true)
                        .Build(),
                    new SlashCommandBuilder()
                        .WithName("askblank")
                        .WithDescription("Ask a question to ChatGPT 4o with out context")
                        .AddOption("question", ApplicationCommandOptionType.String, "Your question", isRequired: true)
                        .Build(),
                    new SlashCommandBuilder()
                        .WithName("asko1")
                        .WithDescription("Ask a question to ChatGPT o1 with context")
                        .AddOption("question", ApplicationCommandOptionType.String, "Your question", isRequired: true)
                        .Build(),
                    new SlashCommandBuilder()
                        .WithName("asko1blank")
                        .WithDescription("Ask a question to ChatGPT o1 with context")
                        .AddOption("question", ApplicationCommandOptionType.String, "Your question", isRequired: true)
                        .Build(),
                    new SlashCommandBuilder()
                        .WithName("timeout")
                        .WithDescription("Timeout a user (Authorized users only)")
                        .AddOption("userid", ApplicationCommandOptionType.String, "User ID to timeout", isRequired: true)
                        .AddOption("duration", ApplicationCommandOptionType.Integer, "Duration in seconds", isRequired: true)
                        .Build(),
                    new SlashCommandBuilder()
                        .WithName("removetimeout")
                        .WithDescription("remove timeout on a user (Authorized users only)")
                        .AddOption("userid", ApplicationCommandOptionType.String, "User ID to timeout", isRequired: true)
                        .Build(),
                    new SlashCommandBuilder()
                        .WithName("addtuserlimit")
                        .WithDescription("Add a user limit to a user (Authorized users only)")
                        .AddOption("userid", ApplicationCommandOptionType.String, "User ID to limit", isRequired: true)
                        .AddOption("limit", ApplicationCommandOptionType.Integer, "Limit", isRequired: true)
                        .Build(),
                    new SlashCommandBuilder()
                        .WithName("removetuserlimit")
                        .WithDescription("Remove a user limit to a user (Authorized users only)")
                        .AddOption("userid", ApplicationCommandOptionType.String, "User ID to limit", isRequired: true)
                        .Build(),
                    new SlashCommandBuilder()
                        .WithName("donate")
                        .WithDescription("info to donate")
                        .Build(),
                    };


            try
            {
                foreach (var command in commands)
                {
                    await _client.CreateGlobalApplicationCommandAsync(command);
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to register global commands: {ex.Message}");
            }

            var managementCommands = new List<ApplicationCommandProperties>
            {
                new SlashCommandBuilder()
                    .WithName("status")
                    .WithDescription("Set the bot's status (Authorized users only)")
                    .AddOption("status", ApplicationCommandOptionType.String, "The status to set", isRequired: true)
                    .Build(),
                new SlashCommandBuilder()
                    .WithName("senddm")
                    .WithDescription("Send a DM to a user (Authorized users only)")
                    .AddOption("userid", ApplicationCommandOptionType.String, "User ID to send DM", isRequired: true)
                    .AddOption("message", ApplicationCommandOptionType.String, "Message to send", isRequired: true)
                    .Build(),
                new SlashCommandBuilder()
                    .WithName("sendmessage")
                    .WithDescription("Send a message to a channel (Authorized users only)")
                    .AddOption("channelid", ApplicationCommandOptionType.String, "Channel ID to send message", isRequired: true)
                    .AddOption("message", ApplicationCommandOptionType.String, "Message to send", isRequired: true)
                    .Build(),
                new SlashCommandBuilder()
                    .WithName("updateprompt")
                    .WithDescription("Update the initial prompt")
                    .Build(),
                new SlashCommandBuilder()
                    .WithName("setMessageHistoryLimit")
                    .WithDescription("Set the message history limit")
                    .AddOption("limit", ApplicationCommandOptionType.Integer, "The limit to set", isRequired: true)
                    .Build()
            };

            try
            {
                var leoGuild = _client.GetGuild(1098268819581055027);
                foreach (var command in managementCommands)
                {
                    await leoGuild.CreateApplicationCommandAsync(command);
                }
            }catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

                // this is faster than the global commands
                //var guilds = _client.Guilds;

            //foreach (var guild in guilds)
            //{
            //    try
            //    {
            //        foreach (var command in commands)
            //        {
            //            await guild.CreateApplicationCommandAsync(command);
            //        }

            //        Console.WriteLine($"Registered commands in guild {guild.Name}");
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine($"Failed to register commands in guild {guild.Name}: {ex.Message}");
            //    }
            //}
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            switch (command.CommandName)
            {
                case "ask":
                    await ProcessChatGPTCommand(command, "gpt-4o");
                    break;
                case "askblank":
                    await ProcessChatGPTCommand(command, "gpt-4o", false);
                    break;
                case "asko1":
                    await command.DeferAsync(); // Acknowledge the command
                    await command.FollowupAsync("Model comming soon. This command is disabled for now");
                    //await ProcessChatGPTCommand(command, "o1-mini");
                    break;
                case "asko1blank":
                    await command.DeferAsync(); // Acknowledge the command
                    await command.FollowupAsync("Model comming soon. This command is disabled for now");
                    //await ProcessChatGPTCommand(command, "o1-mini", false);
                    break;
                case "timeout":
                    await HandleTimeoutCommand(command);
                    break;
                case "removetimeout":
                    await command.DeferAsync(); // Acknowledge the command
                    _timeOuts.Remove(command.User.Id.ToString());
                    await command.FollowupAsync("Timeout removed");
                    break;
                case "addtuserlimit":
                    await command.DeferAsync(); // Acknowledge the command
                    AddUserLimit(command);
                    break;
                case "removetuserlimit":
                    await command.DeferAsync(); // Acknowledge the command
                    _userLimits.Remove(command.Data.Options.First().Value.ToString());
                    break;
                case "status":
                    await command.DeferAsync(); // Acknowledge the command
                    await SetStatus(command.Data.Options.First().Value.ToString());
                    await command.FollowupAsync("Status updated");
                    break;
                case "senddm":
                    await command.DeferAsync(); // Acknowledge the command
                    await SendDM(command);
                    break;
                case "sendmessage":
                    await command.DeferAsync(); // Acknowledge the command
                    await SendMessage(command);
                    break;
                case "updateprompt":
                    await command.DeferAsync(); // Acknowledge the command
                    UpdatePrompt();
                    await command.FollowupAsync("Prompt updated");
                    break;
                case "donate":
                    await command.DeferAsync(); // Acknowledge the command
                    await command.FollowupAsync("If you would like to donate to the bot, you can do so at https://buymeacoffee.com/__trip___");
                    break;
                case "setMessageHistoryLimit":
                    await command.DeferAsync(); // Acknowledge the command
                    var limit = Convert.ToInt32(command.Data.Options.First().Value);
                    MessageHistoryLimit = limit;
                    await command.FollowupAsync($"Message history limit set to {limit}");
                    break;
            }
        }

        private void AddUserLimit(SocketSlashCommand command)
        {
            // Authorization Check
            if (!IsAuthorized((SocketGuildUser)command.User))
            {
                command.FollowupAsync("You are not authorized to use this command.");
                return;
            }

            var userIdOption = command.Data.Options.FirstOrDefault(o => o.Name == "userid")?.Value?.ToString();
            var limitOption = command.Data.Options.FirstOrDefault(o => o.Name == "limit")?.Value;

            if (userIdOption == null || limitOption == null)
            {
                command.FollowupAsync("Invalid arguments.");
                return;
            }

            if (!ulong.TryParse(userIdOption, out ulong userId))
            {
                command.FollowupAsync("Invalid user ID.");
                return;
            }

            var limit = Convert.ToInt32(limitOption);

            _userLimits[userIdOption] = limit;
            command.FollowupAsync($"User {userId} has been limited to {limit} messages.");
        }

        private async Task SendMessage(SocketSlashCommand command)
        {
            // Authorization Check
            if (!IsAuthorized((SocketGuildUser)command.User))
            {
                await command.FollowupAsync("You are not authorized to use this command.");
                return;
            }

            var channelIdOption = command.Data.Options.FirstOrDefault(o => o.Name == "channelid")?.Value?.ToString();
            var messageOption = command.Data.Options.FirstOrDefault(o => o.Name == "message")?.Value?.ToString();

            if (channelIdOption == null || messageOption == null)
            {
                await command.FollowupAsync("Invalid arguments.");
                return;
            }

            if (!ulong.TryParse(channelIdOption, out ulong channelId))
            {
                await command.FollowupAsync("Invalid channel ID.");
                return;
            }

            var channel = _client.GetChannel(channelId) as ISocketMessageChannel;
            if (channel == null)
            {
                await command.FollowupAsync("Channel not found.");
                return;
            }

            try
            {
                await channel.SendMessageAsync(messageOption);
                await command.FollowupAsync("Message sent.");
            }
            catch (Exception ex)
            {
                await command.FollowupAsync($"Failed to send message: {ex.Message}");
            }
        }

        private async Task SendDM(SocketSlashCommand command)
        {
            // Authorization Check
            if (!IsAuthorized((SocketGuildUser)command.User))
            {
                await command.FollowupAsync("You are not authorized to use this command.");
                return;
            }

            var userIdOption = command.Data.Options.FirstOrDefault(o => o.Name == "userid")?.Value?.ToString();
            var messageOption = command.Data.Options.FirstOrDefault(o => o.Name == "message")?.Value?.ToString();

            if (userIdOption == null || messageOption == null)
            {
                await command.FollowupAsync("Invalid arguments.");
                return;
            }

            if (!ulong.TryParse(userIdOption, out ulong userId))
            {
                await command.FollowupAsync("Invalid user ID.");
                return;
            }

            var user = _client.GetUser(userId);
            if (user == null)
            {
                await command.FollowupAsync("User not found.");
                return;
            }

            try
            {
                await user.SendMessageAsync(messageOption);
                await command.FollowupAsync("DM sent.");
            }
            catch (Exception ex)
            {
                await command.FollowupAsync($"Failed to send DM: {ex.Message}");
            }
        }

        private async Task ProcessChatGPTCommand(SocketSlashCommand command, string model, bool context = true)
        {
            await command.DeferAsync(); // Acknowledge the command
            try
            {
                // Check if the user is timed out
                if (_timeOuts.TryGetValue(command.User.Id.ToString(), out DateTime timeout))
                {
                    if (DateTime.Now < timeout)
                    {
                        await command.FollowupAsync("Sorry you are currently timed out");
                        return;
                    }
                    else
                    {
                        _timeOuts.Remove(command.User.Id.ToString());
                    }
                }

                // Check if the user has a message limit
                if (_userLimits.TryGetValue(command.User.Id.ToString(), out int limit))
                {
                    if (limit <= 0)
                    {
                        await command.Channel.SendMessageAsync("Sorry you have reached your message limit");
                        return;
                    }
                    else
                    {
                        _userLimits[command.User.Id.ToString()] = limit - 1;
                    }
                }

                var question = command.Data.Options.First().Value.ToString();

                // Prepare the message history
                var messages = new List<ChatMessage>();

                if (context)
                {
                    // Optionally, you can add system prompts or previous context
                    if (!string.IsNullOrEmpty(initialPrompt))
                    {
                        messages.Add(new SystemChatMessage(initialPrompt));
                    }

                    var channel = command.Channel as SocketGuildChannel;

                    if (command.Channel.GetChannelType() == ChannelType.DM)
                    {
                        messages.Add(new SystemChatMessage($"You are currently in a DM with the {command.User.Username.ToString()}"));
                    }
                    else
                    {
                        messages.Add(new SystemChatMessage($"You are currently in the {channel.Name} channel in the {channel.Guild.Name} server"));
                    }
                    var storageMessages = await DbStorage.GetMessagesByChannelAsync(command.Channel.Name);
                    foreach (var message in storageMessages)
                    {
                        if (message.SentByUser)
                        {
                            messages.Add(new UserChatMessage($"{message.Sender} at {message.SentAt.ToString()} said: {message.Content}"));
                        }
                        else
                        {
                            messages.Add(new AssistantChatMessage($"({message.SentAt.ToString()}) {message.Content}"));
                        }
                    }
                }

                // Add the user's question
                messages.Add(new UserChatMessage($"{command.User.Username} said: {question}"));

                var chat = _openAIClient.GetChatClient(model);

                // Send the messages to ChatGPT
                var result = await chat.CompleteChatAsync(messages);
                var chatResponse = new AssistantChatMessage(result);

                if (chatResponse != null && chatResponse.Content.Count > 0)
                {
                    var responseText = chatResponse.Content[0].Text;

                    // Split response into chunks if necessary
                    var responseChunks = SplitMessage(responseText);

                    DbStorage.AddMessageAsync(new DbStorage.Message
                    {
                        Content = question,
                        Sender = command.User.Username,
                        Server = (command.Channel as SocketGuildChannel)?.Guild.Name,
                        Channel = command.Channel.Name,
                        SentAt = DateTime.Now,
                        SentByUser = true
                    });

                    DbStorage.AddMessageAsync(new DbStorage.Message
                    {
                        Content = responseText,
                        Sender = model,
                        Server = (command.Channel as SocketGuildChannel)?.Guild.Name,
                        Channel = command.Channel.Name,
                        SentAt = DateTime.Now,
                        SentByUser = false
                    });

                    // Send response chunks
                    foreach (var chunk in responseChunks)
                    {
                        await command.FollowupAsync(chunk);
                    }
                }
                else
                {
                    await command.FollowupAsync("I'm sorry, I couldn't generate a response.");
                }
            }
            catch (Exception ex)
            {
                await command.FollowupAsync($"Failed to process command: {ex.Message}");
            }
        }

        private List<string> SplitMessage(string message)
        {
            var chunks = new List<string>();

            for (int i = 0; i < message.Length; i += DiscordMessageLimit)
            {
                int length = Math.Min(DiscordMessageLimit, message.Length - i);
                chunks.Add(message.Substring(i, length));
            }

            return chunks;
        }

        private async Task HandleTimeoutCommand(SocketSlashCommand command)
        {
            await command.DeferAsync(); // Acknowledge the command

            // Authorization Check
            if (!IsAuthorized((SocketGuildUser)command.User))
            {
                await command.FollowupAsync("You are not authorized to use this command.");
                return;
            }

            var userIdOption = command.Data.Options.FirstOrDefault(o => o.Name == "userid")?.Value?.ToString();
            var durationOption = command.Data.Options.FirstOrDefault(o => o.Name == "duration")?.Value;

            if (userIdOption == null || durationOption == null)
            {
                await command.FollowupAsync("Invalid arguments.");
                return;
            }

            if (!ulong.TryParse(userIdOption, out ulong userId))
            {
                await command.FollowupAsync("Invalid user ID.");
                return;
            }

            var duration = Convert.ToInt32(durationOption);

            var guild = (command.Channel as SocketGuildChannel)?.Guild;
            if (guild == null)
            {
                await command.FollowupAsync("Command must be used in a server.");
                return;
            }

            var user = guild.GetUser(userId);
            if (user == null)
            {
                await command.FollowupAsync("User not found.");
                return;
            }

            try
            {
                _timeOuts.Add(user.Id.ToString(), DateTime.Now.AddSeconds(duration));
                await command.FollowupAsync($"User {user.Username} has been timed out for {duration} seconds.");
            }
            catch (Exception ex)
            {
                await command.FollowupAsync($"Failed to timeout user: {ex.Message}");
            }
        }


        private bool IsAuthorized(SocketGuildUser user)
        {
            return user.Id.ToString() == _authorizedUserId || user.GuildPermissions.ManageGuild;
        }

    }
}