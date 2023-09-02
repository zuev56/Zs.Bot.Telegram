using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Telegram.Bot.Types;
using Zs.Common.Extensions;
using DbChat = Zs.Bot.Data.Models.Chat;
using DbUser = Zs.Bot.Data.Models.User;
using DbMessage = Zs.Bot.Data.Models.Message;
using DbChatType = Zs.Bot.Data.Models.ChatType;
using TgChatType = Telegram.Bot.Types.Enums.ChatType;

namespace Zs.Bot.Telegram.Extensions;

internal static class TelegramTypesExtensions
{
    public static DbUser ToDbUser(this User telegramUser)
    {
        var userName = telegramUser.Username.ReplaceEmojiWithX();
        var firstAndLastName = $"{telegramUser.FirstName} {telegramUser.LastName}".Trim().ReplaceEmojiWithX();
        var rawUser = Serialize(telegramUser);

        var user = new DbUser
        {
            Id = telegramUser.Id,
            UserName = userName,
            FullName = !string.IsNullOrWhiteSpace(firstAndLastName) ? firstAndLastName : userName,
            RawData = rawUser
        };

        return user;
    }

    public static DbChat ToDbChat(this Chat telegramChat)
    {
        var chat = new DbChat
        {
            Id = telegramChat.Id,
            Name = (telegramChat.Title ?? telegramChat.Username ?? $"{telegramChat.FirstName} {telegramChat.LastName}").ReplaceEmojiWithX()!,
            Type = ToDbChatType(telegramChat.Type),
            RawData = Serialize(telegramChat)
        };

        return chat;

        static DbChatType ToDbChatType(TgChatType telegramChatType)
        {
            return telegramChatType switch
            {
                TgChatType.Group      => DbChatType.Group,
                TgChatType.Supergroup => DbChatType.Group,
                TgChatType.Channel    => DbChatType.Channel,
                TgChatType.Private    => DbChatType.Private,
                TgChatType.Sender     => DbChatType.Private,
                _ => DbChatType.Undefined
            };
        }
    }

    public static DbMessage ToDbMessage(this Message telegramMessage)
    {
        var message = new DbMessage
        {
            Id = telegramMessage.MessageId,
            ChatId = telegramMessage.Chat.Id,
            UserId = telegramMessage.From!.Id,
            ReplyToMessageId = telegramMessage.ReplyToMessage?.MessageId,
            RawData = Serialize(telegramMessage),
        };

        return message;
    }

    private static string Serialize<TItem>(TItem item)
    {
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        };

        return JsonSerializer.Serialize(item, jsonSerializerOptions).ReplaceEmojiWithX()!;
    }
}