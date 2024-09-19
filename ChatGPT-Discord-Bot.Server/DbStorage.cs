using Google.Cloud.Firestore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChatGPT_Discord_Bot.Server
{
    public class DbStorage
    {
        private FirestoreDb _firestoreDb;
        private const int MaxMessagesPerUser = 20;
        private const int MaxMessageAgeDays = 2;
        private DateTime _serverStartTime;

        // In-memory storage for low-RAM usage statistics
        private ConcurrentDictionary<string, int> _messagesPerUser = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<string, int> _messagesPerChannel = new ConcurrentDictionary<string, int>();
        private int _totalMessages = 0;

        public DbStorage()
        {
            _firestoreDb = FirestoreDb.Create("magnetic-icon-424305-m8");
            _serverStartTime = DateTime.UtcNow;
        }


        // Message model
        public class Message
        {
            public string Content { get; set; }

            public string Sender { get; set; }

            public string Server { get; set; }
            public string Channel { get; set; }

            public DateTime SentAt { get; set; }

            public bool SentByUser { get; set; } // Added boolean field to indicate if the message was sent by the user
        }

        // Add a message to the Firestore
        public async Task AddMessageAsync(Message message)
        {
            CollectionReference messagesRef = _firestoreDb.Collection("messages");

            await messagesRef.AddAsync(new
            {
                Content = message.Content,
                Sender = message.Sender,
                Server = message.Server,
                Channel = message.Channel,
                SentAt = message.SentAt.ToUniversalTime(),
                SentByUser = message.SentByUser
            });
            await EnforceMessageLimitsAsync(message.Sender, message.Channel);

            // Update high-level statistics
            if(message.SentByUser)
            {
                _totalMessages++;
                _messagesPerChannel.AddOrUpdate(message.Channel, 1, (key, value) => value + 1);
                _messagesPerUser.AddOrUpdate(message.Sender, 1, (key, value) => value + 1);
            }
        }

        // Retrieve messages by channel
        public async Task<List<Message>> GetMessagesByChannelAsync(string channel)
        {
            CollectionReference messagesRef = _firestoreDb.Collection("messages");
            Query query = messagesRef.WhereEqualTo("Channel", channel).OrderBy("SentAt");
            QuerySnapshot snapshot = await query.GetSnapshotAsync();

            return snapshot.Documents.Select(doc => new Message
            {
                Content = doc.GetValue<string>("Content"),
                Sender = doc.GetValue<string>("Sender"),
                Server = doc.GetValue<string>("Server"),
                Channel = doc.GetValue<string>("Channel"),
                SentAt = doc.GetValue<DateTime>("SentAt"),
                SentByUser = doc.GetValue<bool>("SentByUser")
            }).ToList();
        }

        // Enforce message limits per user per channel
        private async Task EnforceMessageLimitsAsync(string sender, string channel)
        {
            CollectionReference messagesRef = _firestoreDb.Collection("messages");
            Query query = messagesRef.WhereEqualTo("Sender", sender).WhereEqualTo("Channel", channel)
                .OrderBy("SentAt");
            QuerySnapshot snapshot = await query.GetSnapshotAsync();

            List<DocumentSnapshot> userMessages = snapshot.Documents.ToList();

            // Delete oldest messages if over the limit
            if (userMessages.Count > MaxMessagesPerUser)
            {
                foreach (var messageDoc in userMessages.Take(userMessages.Count - MaxMessagesPerUser))
                {
                    await messageDoc.Reference.DeleteAsync();
                }
            }

            // Delete messages older than 2 days
            DateTime cutoffDate = DateTime.UtcNow.AddDays(-MaxMessageAgeDays);
            foreach (var messageDoc in userMessages)
            {
                if (messageDoc.GetValue<DateTime>("SentAt") < cutoffDate)
                {
                    await messageDoc.Reference.DeleteAsync();
                }
            }
        }

        // Get high-level stats for visualizations
        public int GetTotalMessages() => _totalMessages;

        public Dictionary<string, int> GetMessagesPerUser() => new Dictionary<string, int>(_messagesPerUser);

        public Dictionary<string, int> GetMessagesPerChannel() => new Dictionary<string, int>(_messagesPerChannel);

        // Clear all cached statistics
        public void ClearStatistics()
        {
            _messagesPerUser.Clear();
            _messagesPerChannel.Clear();
            _totalMessages = 0;
        }

        public async void cleardb()
        {
            // clear all records
            QuerySnapshot snapshot = await _firestoreDb.Collection("messages").GetSnapshotAsync();
            foreach (DocumentSnapshot document in snapshot.Documents)
            {
                await document.Reference.DeleteAsync();
            }
        }

        // Get server uptime
        public TimeSpan GetServerUptime() => DateTime.UtcNow - _serverStartTime;
    }
}
