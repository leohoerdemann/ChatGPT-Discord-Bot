using System.Collections.Generic;
using Google.Cloud.Firestore;

namespace ChatGPT_Discord_Bot
{
    public class ConversationEntry
    {
        public string ChannelId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string ServerId { get; set; }
        public string ServerName { get; set; }
        public string Content { get; set; }
        public string Role { get; set; }
        public Timestamp Timestamp { get; set; }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
        {
            { "ChannelId", ChannelId },
            { "UserId", UserId },
            { "UserName", UserName },
            { "ServerId", ServerId },
            { "ServerName", ServerName },
            { "Content", Content },
            { "Role", Role },
            { "Timestamp", Timestamp }
        };
        }
    }
}
