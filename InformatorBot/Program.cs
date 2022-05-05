using InformatorBot;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;

public static class Program
{
    private static TelegramBotClient? Bot;

    public static async Task Main()
    {
        var builder = new ConfigurationBuilder();
        // установка пути к текущему каталогу
        builder.SetBasePath(Directory.GetCurrentDirectory());
        // получаем конфигурацию из файла appsettings.json
        builder.AddJsonFile("appsettings.json");
        // создаем конфигурацию
        var config = builder.Build();

        //Инициализация бота
        Bot = new TelegramBotClient(config["BotToken"]);

        User me = await Bot.GetMeAsync();

        using var cts = new CancellationTokenSource();

        ReceiverOptions receiverOptions = new() { AllowedUpdates = { } };
        Bot.StartReceiving(Handlers.HandleUpdateAsync,
                           Handlers.HandleErrorAsync,
                           receiverOptions,
                           cts.Token);

        Console.WriteLine($"Вас приветствует бот 'Информатор'\nНачинаю логирование!");
        Console.ReadLine();

        // Запрос на отмену операции
        cts.Cancel();
    }
}