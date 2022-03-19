using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Zs.Bot.Data.Factories;
using Zs.Bot.Services.Messaging;
using Zs.Common.Extensions;
using Chat = Telegram.Bot.Types.Chat;
using MessageType = Telegram.Bot.Types.Enums.MessageType;
using TgChatType = Telegram.Bot.Types.Enums.ChatType;
using User = Telegram.Bot.Types.User;

namespace Zs.Bot.Messenger.Telegram
{
    internal sealed class ItemConverter : IToGenegalItemConverter
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        /// <inheritdoc />
        public Zs.Bot.Data.Models.Message ToGeneralMessage(object specificMessage)
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
                message.RawDataHash = message.RawData.GetMD5Hash();
                message.IsSucceed = telegramMessage.IsSucceed;
                message.FailsCount = telegramMessage.SendingFails;
                message.FailDescription = telegramMessage.FailDescription;
                message.ReplyToMessageId = null; // define later
                return message;
            }

            throw new InvalidCastException($"{nameof(specificMessage)} is not a {typeof(TgMessage).FullName}");
        }

        /// <inheritdoc />
        public Zs.Bot.Data.Models.Chat ToGeneralChat(object specificChat)
        {
            if (specificChat is Chat telegramChat)
            {
                var chat = EntityFactory.NewChat();
                var serializedChat = JsonSerializer.Serialize(telegramChat, _jsonSerializerOptions);

                //chat.ChatId -> Auto
                chat.Description = telegramChat.Description.ReplaceEmojiWithX();
                chat.Name = (telegramChat.Title ?? telegramChat.Username ?? $"{telegramChat.FirstName} {telegramChat.LastName}").ReplaceEmojiWithX();
                chat.ChatTypeId = ToGeneralChatType(telegramChat.Type).ToString().ToUpperInvariant();
                chat.RawData = serializedChat.NormalizeJsonString().ReplaceEmojiWithX();
                chat.RawDataHash = chat.RawData.GetMD5Hash();

                return chat;
            }
            else
                throw new InvalidCastException($"{nameof(specificChat)} is not a {typeof(Chat).FullName}");
        }

        /// <inheritdoc />
        public Zs.Bot.Data.Models.User ToGeneralUser(object specificUser)
        {
            if (specificUser is User telegramUser)
            {
                var user = EntityFactory.NewUser();
                var serializedUser = JsonSerializer.Serialize(telegramUser, _jsonSerializerOptions);

                //user.UserId -> Auto
                user.Name = telegramUser.Username.ReplaceEmojiWithX();
                user.FullName = ($"{telegramUser.FirstName} {telegramUser.LastName}").Trim().ReplaceEmojiWithX();
                user.IsBot = telegramUser.IsBot;
                user.RawData = serializedUser.NormalizeJsonString().ReplaceEmojiWithX();
                user.RawDataHash = user.RawData.GetMD5Hash();

                return user;
            }

            throw new InvalidCastException($"{nameof(specificUser)} is not a {typeof(User).FullName}");
        }

        /// <inheritdoc />
        public Zs.Bot.Data.Enums.ChatType ToGeneralChatType(object specificChatType)
        {
            if (specificChatType is TgChatType chatType)
            {
                return chatType switch
                {
                    TgChatType.Group      => Zs.Bot.Data.Enums.ChatType.Group,
                    TgChatType.Supergroup => Zs.Bot.Data.Enums.ChatType.Group,
                    TgChatType.Channel    => Zs.Bot.Data.Enums.ChatType.Channel,
                    TgChatType.Private    => Zs.Bot.Data.Enums.ChatType.Private,
                    _ => Zs.Bot.Data.Enums.ChatType.Undefined
                };
            }

            return Zs.Bot.Data.Enums.ChatType.Undefined;
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
                s == MessageType.ProximityAlertTriggered ||
                s == MessageType.VoiceChatScheduled ||
                s == MessageType.VoiceChatStarted ||
                s == MessageType.VoiceChatEnded ||
                s == MessageType.VoiceChatParticipantsInvited => "SRV",

                _ => "UKN"


                
            };
        }
    }
}
