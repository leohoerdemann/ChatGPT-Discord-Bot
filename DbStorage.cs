using Google.Cloud.Firestore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChatGPT_Discord_Bot;
using OpenAI.Chat;

namespace ChatGPT_Discord_Bot
{
    public class DbStorage
    {
        private readonly FirestoreDb _firestoreDb;

        public DbStorage()
        {
            var projectId = "magnetic-icon-424305-m8"; // Replace with your Google Cloud project ID
            _firestoreDb = FirestoreDb.Create(projectId);
        }

        public async Task<List<ChatMessage>> GetContextMessagesAsync(ulong channelId)
        {
            var query = _firestoreDb.Collection("messages")
                .WhereEqualTo("ChannelId", channelId.ToString())
                .OrderByDescending("Timestamp")
                .Limit(20);

            var snapshot = await query.GetSnapshotAsync();
            var messages = new List<ChatMessage>();

            foreach (var doc in snapshot.Documents.Reverse())
            {
                var role = doc.GetValue<string>("Role");
                var content = doc.GetValue<string>("Content");
                if (role == "User")
                {
                    var message = new UserChatMessage(content);
                    messages.Add(message);
                }
                else
                {
                    var message = new AssistantChatMessage(content);
                    messages.Add(message);
                }
            }

            return messages;
        }

        public async Task SaveConversationAsync(ConversationEntry userMessage, ConversationEntry botMessage)
        {
            var batch = _firestoreDb.StartBatch();
            batch.Create(_firestoreDb.Collection("messages").Document(), userMessage.ToDictionary());
            batch.Create(_firestoreDb.Collection("messages").Document(), botMessage.ToDictionary());
            await batch.CommitAsync();
        }

        public async Task<object> GetStatsAsync()
        {
            var messagesCollection = _firestoreDb.Collection("messages");
            var snapshot = await messagesCollection.GetSnapshotAsync();

            var totalMessages = snapshot.Count;

            var messagesByUser = snapshot.Documents
                .Where(doc => doc.ContainsField("UserName"))
                .GroupBy(doc => doc.GetValue<string>("UserName"))
                .Select(group => new { UserName = group.Key, MessageCount = group.Count() })
                .ToList();

            var messagesByServer = snapshot.Documents
                .Where(doc => doc.ContainsField("ServerName"))
                .GroupBy(doc => doc.GetValue<string>("ServerName"))
                .Select(group => new { ServerName = group.Key, MessageCount = group.Count() })
                .ToList();

            return new
            {
                TotalMessages = totalMessages,
                MessagesByUser = messagesByUser,
                MessagesByServer = messagesByServer
            };
        }
    }
}
