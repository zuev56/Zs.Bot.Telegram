using System.Linq;
using System.Text.Json;
using Zs.Bot.Data.Models;

namespace Zs.Bot.Telegram.Extensions;

public static class MessageExtensions
{
    public static string? GetText(this Message message)
    {
        var messageJson = JsonSerializer.Deserialize<JsonElement>(message.RawData);
        var textProperty = messageJson.EnumerateObject().Where(static i => i.Name == "Text").ToArray();
        if (!textProperty.Any())
            return null;

        return textProperty.Single().Value.ToString();
    }
}