using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChatGPT_Discord_Bot;
using Google.Cloud.Firestore;
using OpenAI.Chat;
using OpenAI;

namespace ChatGPT_Discord_Bot
{
    public class BotLogic
    {
        private readonly DiscordSocketClient _client;
        private OpenAIClient _openAiClient;
        private readonly DbStorage _dbStorage;
        private string _authorizedUserId;
        private const int DiscordMessageLimit = 2000;

        public BotLogic()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
                UseInteractionSnowflakeDate = false
            });

            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.SlashCommandExecuted += SlashCommandHandler;

            _dbStorage = new DbStorage();
        }

        public async Task InitializeAsync()
        {
            var discordToken = await GoogleSecrets.GetDiscordToken();
            await _client.LoginAsync(TokenType.Bot, discordToken);
            await _client.StartAsync();

            var openAiApiKey = await GoogleSecrets.GetOpenAIKey();

            // Instantiate the OpenAIClient
            _openAiClient = new OpenAIClient(openAiApiKey);

            _authorizedUserId = await GoogleSecrets.GetAdminID();
        }

        private Task LogAsync(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task ReadyAsync()
        {
            // Register Slash Commands
            var guilds = _client.Guilds;

            foreach (var guild in guilds)
            {
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
                        .WithName("timeout")
                        .WithDescription("Timeout a user (Authorized users only)")
                        .AddOption("userid", ApplicationCommandOptionType.String, "User ID to timeout", isRequired: true)
                        .AddOption("duration", ApplicationCommandOptionType.Integer, "Duration in seconds", isRequired: true)
                        .Build()
                    // Additional commands can be added here
                };

                foreach (var command in commands)
                {
                    await guild.CreateApplicationCommandAsync(command);
                }
            }
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
                    await ProcessChatGPTCommand(command, "o1-preview");
                    break;
                case "timeout":
                    await HandleTimeoutCommand(command);
                    break;
            }
        }

        private async Task ProcessChatGPTCommand(SocketSlashCommand command, string model, bool context = true)
        {
            await command.DeferAsync(); // Acknowledge the command

            var question = command.Data.Options.First().Value.ToString();

            // Prepare the message history
            var messages = new List<ChatMessage>();

            // Optionally, you can add system prompts or previous context
            var initialPrompt = await GoogleSecrets.GetPrompt();
            if (!string.IsNullOrEmpty(initialPrompt))
            {
                messages.Add(new SystemChatMessage(initialPrompt));
            }

            // Add the user's question
            messages.Add(new UserChatMessage(question));

            var chat = _openAiClient.GetChatClient(model);

            // Send the messages to ChatGPT
            var result = await chat.CompleteChatAsync(messages);
            var chatResponse = new AssistantChatMessage(result);

            if (chatResponse != null && chatResponse.Content.Count > 0)
            {
                var responseText = chatResponse.Content[0].Text;

                // Split response into chunks if necessary
                var responseChunks = SplitMessage(responseText);

                // Send response chunks
                foreach (var chunk in responseChunks)
                {
                    await command.FollowupAsync(chunk);
                }

                // Save conversation to Firestore
                var userMessage = new ConversationEntry
                {
                    ChannelId = command.Channel.Id.ToString(),
                    UserId = command.User.Id.ToString(),
                    UserName = command.User.Username,
                    ServerId = (command.Channel as SocketGuildChannel)?.Guild.Id.ToString(),
                    ServerName = (command.Channel as SocketGuildChannel)?.Guild.Name,
                    Content = question,
                    Role = "user",
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
                };

                var botMessage = new ConversationEntry
                {
                    ChannelId = command.Channel.Id.ToString(),
                    UserId = _client.CurrentUser.Id.ToString(),
                    UserName = _client.CurrentUser.Username,
                    ServerId = (command.Channel as SocketGuildChannel)?.Guild.Id.ToString(),
                    ServerName = (command.Channel as SocketGuildChannel)?.Guild.Name,
                    Content = responseText,
                    Role = "assistant",
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
                };

                await _dbStorage.SaveConversationAsync(userMessage, botMessage);
            }
            else
            {
                await command.FollowupAsync("I'm sorry, I couldn't generate a response.");
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
            if (command.User.Id.ToString() != _authorizedUserId)
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
                await user.SetTimeOutAsync(TimeSpan.FromSeconds(duration));
                await command.FollowupAsync($"User {user.Username} has been timed out for {duration} seconds.");
            }
            catch (Exception ex)
            {
                await command.FollowupAsync($"Failed to timeout user: {ex.Message}");
            }
        }
    }
}
