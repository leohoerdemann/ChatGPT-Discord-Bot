namespace ChatGPT_Discord_Bot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() { Title = "My API", Version = "v1" });
            });

            var app = builder.Build();

            var leobot = new BotLogic();
            leobot.Start().GetAwaiter().GetResult();

            app.MapPut("/sendDm/{user}/{message}", (string user, string message) =>
            {
                leobot.sendDM(message, user);
                return Results.Ok();
            });

            app.MapPut("/sendman/{message}/{channel}", (string message, string channel) =>
            {
                leobot.sendManual(message, channel);
                return Results.Ok();
            });

            app.MapGet("/test", () =>
            {
                return Results.Ok("Hello World!");
            });



            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();


            app.Run();
        }
    }
}