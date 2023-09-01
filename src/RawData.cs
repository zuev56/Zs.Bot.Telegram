using Zs.Bot.Data.Queries;

namespace Zs.Bot.Telegram;

public static class RawData
{
    public static RawDataStructure Structure => new()
    {
        Chat = new RawChatPaths
        {
            Id = "$.Id",
            Name = "$.Username"
        },
        User = new RawUserPaths
        {
            Id = "$.Id",
            Name = "$.Username",
            IsBot = "$.IsBot"
        },
        Message = new RawMessagePaths
        {
            Id = "$.MessageId",
            Text = "$.Text",
            ChatId = "$.Chat.Id",
            UserId = "$.From.Id",
            Date = "$.Date"
        }
    };
}