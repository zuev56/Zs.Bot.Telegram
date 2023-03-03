using System;
using System.Threading.Tasks;
using Telegram.Bot;
using Zs.Bot.Data.Enums;
using Zs.Bot.Data.Models;
using Zs.Bot.Services.Messaging;

namespace Zs.Bot.Messenger.Telegram;

internal interface IOutputMessageProcessor
{
    ITelegramBotClient BotClient { get; }
    public event EventHandler<MessageActionEventArgs>? MessageProcessed;
    Task<bool> DeleteMessageAsync(Message dbMessage);
    void EnqueueMessage(Chat chat, string messageText, Message? messageToReply = null);
    void EnqueueMessage(int chatId, string text);
    Task EnqueueMessageAsync(string messageText, params Role[] userRoles);
}