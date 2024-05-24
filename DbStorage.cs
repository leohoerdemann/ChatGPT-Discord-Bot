using Google.Cloud.Firestore;

namespace ChatGPT_Discord_Bot
{
    public static class DbStorage
    {
        public static FirestoreDb db = FirestoreDb.Create("LeoBot");


        public static async Task AddChannel(string channelID)
        {
            DocumentReference docRef = db.Collection("channels").Document(channelID);
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                { "channelID", channelID }
            };
            await docRef.SetAsync(data);
        }

        public static async Task RemoveChannel(string channelID)
        {
            DocumentReference docRef = db.Collection("channels").Document(channelID);
            await docRef.DeleteAsync();
        }

        public static async Task<bool> CheckChannel(string channelID)
        {
            DocumentReference docRef = db.Collection("channels").Document(channelID);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
            return snapshot.Exists;
        }

        public static async Task BanUser(string userID)
        {
            DocumentReference docRef = db.Collection("ban_users").Document(userID);
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                { "userID", userID }
            };
            await docRef.SetAsync(data);
        }

        public static async Task UnbanUser(string userID)
        {
            DocumentReference docRef = db.Collection("ban_users").Document(userID);
            await docRef.DeleteAsync();
        }

        public static async Task<bool> CheckBanUser(string userID)
        {
            DocumentReference docRef = db.Collection("ban_users").Document(userID);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
            return snapshot.Exists;
        }

        public static async Task AddMessage(string channelID, string User, string content)
        {
            DocumentReference documentRef = db.Collection("messages").Document();
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                { "channelID", channelID },
                { "User", User },
                { "content", content },
                { "timestamp", FieldValue.ServerTimestamp }
            };

            await documentRef.SetAsync(data);
        }   

        public static async Task<List<string>> GetMessages(string channelID)
        {
            Query query = db.Collection("messages").WhereEqualTo("channelID", channelID).OrderBy("timestamp").Limit(15);
            QuerySnapshot querySnapshot = await query.GetSnapshotAsync();
            List<string> messages = new List<string>();

            foreach (DocumentSnapshot documentSnapshot in querySnapshot.Documents)
            {
                Dictionary<string, object> document = documentSnapshot.ToDictionary();
                string message = document["User"] + ": " + document["content"];
                messages.Add(message);
            }

            return messages;
        }

        public static async Task RemoveOldMessages(string channelID)
        {
            // remove messages older than 20 minutes
            Query query = db.Collection("messages").WhereEqualTo("channelID", channelID).WhereGreaterThan("timestamp", DateTime.UtcNow.AddMinutes(-20));
            QuerySnapshot querySnapshot = await query.GetSnapshotAsync();
            
            foreach (DocumentSnapshot documentSnapshot in querySnapshot.Documents)
            {
                await documentSnapshot.Reference.DeleteAsync();
            }
        }

    }
}
