namespace ChatGPT_Discord_Bot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddRazorPages();

            var app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();
            // Configure the HTTP request pipeline.
            app.UseStaticFiles();
            app.UseRouting();
            app.MapRazorPages();


            var leobot = new BotLogic();
            leobot.InitializeAsync();


            //app.MapPut("/sendDm/{user}/{message}", (string user, string message) =>
            //{
            //    leobot.sendDM(message, user);
            //    return Results.Ok();
            //});

            //app.MapPut("/sendman/{message}/{channel}", (string message, string channel) =>
            //{
            //    leobot.sendManual(message, channel);
            //    return Results.Ok();
            //});

            app.MapGet("/test", () =>
            {
                return Results.Ok("Hello World!");
            }).WithOpenApi();

            app.MapGet("/stats/data", async () =>
            {
                var dbStorage = new DbStorage();
                var stats = await dbStorage.GetStatsAsync();
                return Results.Json(stats);
            });


            app.RunAsync();
        }
    }
}