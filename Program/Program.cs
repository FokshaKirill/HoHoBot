using HoHoBot.Application.Services;
using HoHoBot.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

static string LoadToken(string path)
{
    try
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Файл токена не найден: {path}");

        return File.ReadAllText(path).Trim();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при загрузке токена: {ex.Message}");
        throw;
    }
}

var tokenPath = @"G:\Study\GitHub\Dex\materials\telegramAPI.txt";
string botToken = LoadToken(tokenPath);

var builder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

IConfiguration configuration = builder.Build();

var serviceProvider = new ServiceCollection()
    .AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken)) 
    .AddDbContext<HoHoBotDbContext>(options =>
        options.UseSqlite(configuration.GetConnectionString("DefaultConnection")))
    .AddSingleton<BotService>()
    .BuildServiceProvider();

using (var scope = serviceProvider.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<HoHoBotDbContext>();
    dbContext.Database.EnsureCreated();
}

var botClient = serviceProvider.GetRequiredService<ITelegramBotClient>();
var botService = serviceProvider.GetRequiredService<BotService>();

using var cts = new CancellationTokenSource();

botClient.StartReceiving(
    updateHandler: async (client, update, token) => await botService.HandleUpdateAsync(update),
    errorHandler: async (client, exception, token) =>
    {
        Console.WriteLine($"Polling error: {exception.Message}");
        await Task.CompletedTask;
    },
    cancellationToken: cts.Token
);

Console.WriteLine("Bot is running... Press any key to exit.");
Console.ReadKey();

cts.Cancel();
