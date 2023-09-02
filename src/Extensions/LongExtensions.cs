using Zs.Bot.Data.Models;

namespace Zs.Bot.Telegram.Extensions;

public static class LongExtensions
{
    public static Chat ToChat(this long telegramChatId)
    {
        return new Chat
        {
            Id = telegramChatId,
            RawData = $"{{ \"Id\": {telegramChatId} }}"
        };
    }
}