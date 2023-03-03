using System;
using Zs.Bot.Services.Messaging;

namespace Zs.Bot.Messenger.Telegram;

internal interface IInputMessageProcessor
{
    public event EventHandler<MessageActionEventArgs>? MessageProcessed;
    void EnqueueMessage(TgMessage tgMessage, out MessageActionEventArgs eventArgs);
}