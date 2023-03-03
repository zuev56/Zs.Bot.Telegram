using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Zs.Bot.Data.Factories;
using Zs.Bot.Data.Models;
using Zs.Bot.Services.Messaging;
using Zs.Common.Extensions;
using ChatType = Zs.Bot.Data.Enums.ChatType;
using MessageType = Telegram.Bot.Types.Enums.MessageType;
using TgChatType = Telegram.Bot.Types.Enums.ChatType;

namespace Zs.Bot.Messenger.Telegram;

internal sealed class ItemConverter : IToGeneralItemConverter
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public Message ToGeneralMessage(object specificMessage)
    {
        if (specificMessage is TgMessage telegramMessage)
        {
            var message = EntityFactory.NewMessage();
            var serializedMessage = JsonSerializer.Serialize(telegramMessage, _jsonSerializerOptions);

            //message.MessageId     -> Auto
            //message.ChatId        -> define when saving
            //message.UserId        -> define when saving
            message.MessengerId = "TG";
            message.MessageTypeId = GetGeneralMessageTypeId(telegramMessage.Type);
            message.Text = telegramMessage.Text.ReplaceEmojiWithX();
            message.RawData = serializedMessage.NormalizeJsonString().ReplaceEmojiWithX();
            message.RawDataHash = message.RawData!.GetMd5Hash();
            message.IsSucceed = telegramMessage.IsSucceed;
            message.FailsCount = telegramMessage.SendingFails;
            message.FailDescription = telegramMessage.FailDescription;
            message.ReplyToMessageId = null; // define later
            return message;
        }

        throw new InvalidCastException($"{nameof(specificMessage)} is not a {typeof(TgMessage).FullName}");
    }

    public Chat ToGeneralChat(object specificChat)
    {
        if (specificChat is global::Telegram.Bot.Types.Chat telegramChat)
        {
            var chat = EntityFactory.NewChat();
            var serializedChat = JsonSerializer.Serialize(telegramChat, _jsonSerializerOptions);

            //chat.ChatId -> Auto
            chat.Description = telegramChat.Description.ReplaceEmojiWithX();
            chat.Name = (telegramChat.Title ?? telegramChat.Username ?? $"{telegramChat.FirstName} {telegramChat.LastName}").ReplaceEmojiWithX();
            chat.ChatTypeId = ToGeneralChatType(telegramChat.Type).ToString().ToUpperInvariant();
            chat.RawData = serializedChat.NormalizeJsonString().ReplaceEmojiWithX();
            chat.RawDataHash = chat.RawData!.GetMd5Hash();

            return chat;
        }

        throw new InvalidCastException($"{nameof(specificChat)} is not a {typeof(global::Telegram.Bot.Types.Chat).FullName}");
    }

    public User ToGeneralUser(object specificUser)
    {
        if (specificUser is global::Telegram.Bot.Types.User telegramUser)
        {
            var user = EntityFactory.NewUser();
            var serializedUser = JsonSerializer.Serialize(telegramUser, _jsonSerializerOptions);

            //user.UserId -> Auto
            user.Name = telegramUser.Username.ReplaceEmojiWithX();
            user.FullName = ($"{telegramUser.FirstName} {telegramUser.LastName}").Trim().ReplaceEmojiWithX();
            user.IsBot = telegramUser.IsBot;
            user.RawData = serializedUser.NormalizeJsonString().ReplaceEmojiWithX();
            user.RawDataHash = user.RawData.GetMd5Hash();

            return user;
        }

        throw new InvalidCastException($"{nameof(specificUser)} is not a {typeof(global::Telegram.Bot.Types.User).FullName}");
    }

    public ChatType ToGeneralChatType(object specificChatType)
    {
        if (specificChatType is TgChatType chatType)
        {
            return chatType switch
            {
                TgChatType.Group      => ChatType.Group,
                TgChatType.Supergroup => ChatType.Group,
                TgChatType.Channel    => ChatType.Channel,
                TgChatType.Private    => ChatType.Private,
                _ => ChatType.Undefined
            };
        }

        return ChatType.Undefined;
    }

    private static string GetGeneralMessageTypeId(MessageType type)
    {
        return type switch
        {
            MessageType.Text     => "TXT",
            MessageType.Photo    => "PHT",
            MessageType.Audio    => "AUD",
            MessageType.Video    => "VID",
            MessageType.Voice    => "VOI",
            MessageType.Document => "DOC",
            MessageType.Sticker  => "STK",
            MessageType.Location => "LOC",
            MessageType.Contact  => "CNT",

            var o when
                o == MessageType.Venue ||
                o == MessageType.Game ||
                o == MessageType.VideoNote ||
                o == MessageType.Invoice ||
                o == MessageType.SuccessfulPayment ||
                o == MessageType.WebsiteConnected ||
                o == MessageType.Poll ||
                o == MessageType.Dice => "OTH",

            var s when
                s == MessageType.ChatMembersAdded ||
                s == MessageType.ChatMemberLeft ||
                s == MessageType.ChatTitleChanged ||
                s == MessageType.ChatPhotoChanged ||
                s == MessageType.MessagePinned ||
                s == MessageType.ChatPhotoDeleted ||
                s == MessageType.GroupCreated ||
                s == MessageType.SupergroupCreated ||
                s == MessageType.ChannelCreated ||
                s == MessageType.MigratedToSupergroup ||
                s == MessageType.MigratedFromGroup ||
                s == MessageType.Unknown ||
                s == MessageType.MessageAutoDeleteTimerChanged ||
                s == MessageType.ProximityAlertTriggered => "SRV",

            _ => "UKN"
        };
    }
}