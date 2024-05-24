using Google.Cloud.SecretManager.V1;

namespace ChatGPT_Discord_Bot
{
    public static class GoogleSecrets
    {
        private static SecretManagerServiceClient client = SecretManagerServiceClient.Create();


        public static async Task<string> GetSecret(string secretId)
        {
            SecretVersionName secretVersionName = new SecretVersionName("magnetic-icon-424305-m8", secretId, "latest");
            AccessSecretVersionResponse result = await client.AccessSecretVersionAsync(secretVersionName);
            return result.Payload.Data.ToStringUtf8();
        }

        public async static Task<string> GetDiscordToken()
        {
            return await GetSecret("DiscordToken");
        }

        public async static Task<string> GetOpenAIKey()
        {
            return await GetSecret("OpenAIKey");
        }

        public async static Task<string> GetPrompt()
        {
            return await GetSecret("Prompt");
        }

        public async static Task<string> GetAdminID()
        {
            return await GetSecret("AdminID");
        }
    }
}
