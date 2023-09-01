using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Zs.Bot.Services.Messaging;

namespace Zs.Bot.Telegram.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramBotClient(this IServiceCollection services, string token)
    {
        services.AddSingleton(RawData.Structure);

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<BotClient>>();
        var telegramBotClientOptions = new TelegramBotClientOptions(token);
        var telegramBotClient = new TelegramBotClient(telegramBotClientOptions);
        var botClient = new BotClient(telegramBotClient, logger);

        return services.AddSingleton<IBotClient>(botClient);
    }
}