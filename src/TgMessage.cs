using System;
using System.Text.Json.Serialization;
using Telegram.Bot.Types;

namespace Zs.Bot.Messenger.Telegram;

/// <summary>
/// Обёртка входящего сообщения Telegram.
/// Сделана для расширения состояния и сокращения полей при сериализации
/// </summary>
public sealed class TgMessage : Message
{
    [JsonIgnore]
    internal new User? From { get => base.From; set => base.From = value; }

    [JsonIgnore]
    internal new User? ForwardFrom { get => base.ForwardFrom; set => base.ForwardFrom = value; }

    [JsonIgnore]
    internal new int? ForwardFromMessageId { get => base.ForwardFromMessageId; set => base.ForwardFromMessageId = value; }

    [JsonIgnore]
    internal new Chat Chat { get => base.Chat; set => base.Chat = value; }

    [JsonIgnore]
    internal new Chat? ForwardFromChat { get => base.ForwardFromChat; set => base.ForwardFromChat = value; }

    [JsonIgnore]
    internal new Message? ReplyToMessage { get => base.ReplyToMessage; set => base.ReplyToMessage = value; }

    [JsonIgnore]
    internal new Message? PinnedMessage { get => base.PinnedMessage; set => base.PinnedMessage = value; }

    [JsonIgnore]
    internal new bool? DeleteChatPhoto { get => base.DeleteChatPhoto; set => base.DeleteChatPhoto = value; }

    [JsonIgnore]
    internal new bool? GroupChatCreated { get => base.GroupChatCreated; set => base.GroupChatCreated = value; }

    [JsonIgnore]
    internal new bool? SupergroupChatCreated { get => base.SupergroupChatCreated; set => base.SupergroupChatCreated = value; }

    [JsonIgnore]
    internal new bool? ChannelChatCreated { get => base.ChannelChatCreated; set => base.ChannelChatCreated = value; }

    [JsonIgnore]
    internal new long? MigrateToChatId { get => base.MigrateToChatId; set => base.MigrateToChatId = value; }

    [JsonIgnore]
    internal new long? MigrateFromChatId { get => base.MigrateFromChatId; set => base.MigrateFromChatId = value; }

    /// <summary>Count message sending fails</summary>
    [JsonIgnore]
    internal int SendingFails { get; set; }

    /// <summary>Describes sending fail</summary>
    [JsonIgnore]
    internal string FailDescription { get; set; }

    [JsonIgnore]
    internal bool IsSucceed { get; set; }

    internal long? FromId => base.From?.Id;
    internal long? ForwardFromId => base.ForwardFrom?.Id;
    internal long ChatId => base.Chat.Id;
    internal long? ForwardFromChatId => base.ForwardFromChat?.Id;
    internal int? ReplyToMessageId => base.ReplyToMessage?.MessageId;
    internal int? PinnedMessageId => base.PinnedMessage?.MessageId;



    internal TgMessage(Message msg)
    {
        Parse(msg ?? throw new ArgumentNullException(nameof(msg)));

        Date = Date == default ? DateTime.UtcNow : Date;
    }

    internal TgMessage(Chat chat, string msgText)
    {
        Parse(new Message
        {
            Chat = chat ?? throw new ArgumentNullException(nameof(chat)),
            Text = msgText
        });

        Date = DateTime.UtcNow;
    }


    /// <summary>Fill </summary>
    internal void Parse(Message msg)
    {
        Animation             = msg.Animation;
        Audio                 = msg.Audio;
        AuthorSignature       = msg.AuthorSignature;
        Caption               = msg.Caption;
        CaptionEntities       = msg.CaptionEntities;
        ChannelChatCreated    = msg.ChannelChatCreated;
        base.Chat             = msg.Chat;
        ConnectedWebsite      = msg.ConnectedWebsite;
        Contact               = msg.Contact;
        Date                  = msg.Date;
        DeleteChatPhoto       = msg.DeleteChatPhoto;
        Document              = msg.Document;
        EditDate              = msg.EditDate;
        Entities              = msg.Entities;
        ForwardDate           = msg.ForwardDate;
        base.ForwardFrom      = msg.ForwardFrom;
        base.ForwardFromChat  = msg.ForwardFromChat;
        ForwardFromMessageId  = msg.ForwardFromMessageId;
        ForwardSignature      = msg.ForwardSignature;
        base.From             = msg.From;
        Game                  = msg.Game;
        GroupChatCreated      = msg.GroupChatCreated;
        Invoice               = msg.Invoice;
        LeftChatMember        = msg.LeftChatMember;
        Location              = msg.Location;
        MediaGroupId          = msg.MediaGroupId;
        MessageId             = msg.MessageId;
        MigrateFromChatId     = msg.MigrateFromChatId;
        MigrateToChatId       = msg.MigrateToChatId;
        NewChatMembers        = msg.NewChatMembers;
        NewChatPhoto          = msg.NewChatPhoto;
        NewChatTitle          = msg.NewChatTitle;
        Photo                 = msg.Photo;
        PinnedMessage         = msg.PinnedMessage;
        ReplyToMessage        = msg.ReplyToMessage;
        Sticker               = msg.Sticker;
        SuccessfulPayment     = msg.SuccessfulPayment;
        SupergroupChatCreated = msg.SupergroupChatCreated;
        Text                  = msg.Text;
        Venue                 = msg.Venue;
        Video                 = msg.Video;
        VideoNote             = msg.VideoNote;
        Voice                 = msg.Voice;
    }
}