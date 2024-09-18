
namespace ChatGPT_Discord_Bot.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapFallbackToFile("/index.html");

            var leoBot = new BotLogic();
            leoBot.InitializeAsync();

            app.MapPut("/status/{status}", (string status) =>
            {
                leoBot.SetStatus(status);
                return Results.Ok();
            });

            var dbStorage = leoBot.DbStorage;

            app.MapGet("/api/stats/messages/total", () => Results.Json(new { totalMessages = dbStorage.GetTotalMessages() }));

            app.MapGet("/api/stats/messages/user", () => Results.Json(dbStorage.GetMessagesPerUser()));

            app.MapGet("/api/stats/messages/channel", () => Results.Json(dbStorage.GetMessagesPerChannel()));

            app.MapGet("/api/stats/server/uptime", () => Results.Json(new { uptime = dbStorage.GetServerUptime().ToString() }));

            app.MapPost("/api/stats/clear", () =>
            {
                dbStorage.ClearStatistics();
                return Results.Ok("Statistics cleared.");
            });


            app.Run();
        }
    }
}
