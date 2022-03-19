using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Zs.Bot.Data.Abstractions;
using Zs.Bot.Data.Enums;
using Zs.Bot.Data.Models;
using Zs.Bot.Services.Messaging;
using Zs.Common.Models;

namespace Zs.Bot.Messenger.Telegram;

internal sealed class InputMessageProcessor : IInputMessageProcessor
{
    private readonly IChatsRepository _chatsRepo;
    private readonly IUsersRepository _usersRepo;
    private readonly IMessagesRepository _messagesRepo;
    private readonly IToGenegalItemConverter _itemConverter = new ItemConverter();
    private readonly ILogger<InputMessageProcessor> _logger;

    private readonly Buffer<TgMessage> _inputMessageBuffer = new();

    public event EventHandler<MessageActionEventArgs> MessageProcessed;

    public InputMessageProcessor(
        IChatsRepository chatsRepo,
        IUsersRepository usersRepo,
        IMessagesRepository messagesRepo,
        ILogger<InputMessageProcessor> logger = null
        )
    {
        _chatsRepo = chatsRepo ?? throw new ArgumentNullException(nameof(chatsRepo));
        _usersRepo = usersRepo ?? throw new ArgumentNullException(nameof(usersRepo));
        _messagesRepo = messagesRepo ?? throw new ArgumentNullException(nameof(messagesRepo));

        _logger = logger;

        _inputMessageBuffer.OnEnqueue += InputMessageBuffer_OnEnqueue;
    }

    public bool EnqueueMessage(TgMessage tgMessage, out MessageActionEventArgs eventArgs)
    {
        try
        {
            if (tgMessage is null)
                throw new ArgumentNullException(nameof(tgMessage));

            _inputMessageBuffer.Enqueue(tgMessage);

            eventArgs = CreateMessageActionEventArgs(tgMessage, MessageAction.Received);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Adding message to inbox error");
            eventArgs = null;
            return false;
        }
    }

    private void InputMessageBuffer_OnEnqueue(object sender, TgMessage item)
    {
        Task.Run(() => ProcessInputMessages());
    }

    private async Task ProcessInputMessages()
    {
        TgMessage msgForLog = null;

        try
        {
            while (_inputMessageBuffer.TryDequeue(out TgMessage tgMessage))
            {
                msgForLog = tgMessage;
                tgMessage.IsSucceed = true;

                if (tgMessage.EditDate is not null)
                {
                    var args = CreateMessageActionEventArgs(tgMessage, MessageAction.Edited);

                    if (await TrySetExistingMessageId(args.Message))
                        Volatile.Read(ref MessageProcessed)?.Invoke(this, args);
                    else
                        _logger?.LogWarning("The message is not found in the database (Message: {Message})", tgMessage);
                }
                else
                {
                    var args = CreateMessageActionEventArgs(tgMessage, MessageAction.Received);

                    await TrySetExistingUserIdAndRole(args.User).ConfigureAwait(false);
                    await TrySetExistingChatId(args.Chat).ConfigureAwait(false);

                    Volatile.Read(ref MessageProcessed)?.Invoke(this, args);
                }

                msgForLog = null;
            }
        }
        catch (Exception ex)
        {
            LogInputMessageProcessingError(msgForLog, ex);
        }
    }

    private void LogInputMessageProcessingError(TgMessage msgForLog, Exception ex)
    {
        string json;
        try
        {
            json = _itemConverter.ToGeneralMessage(msgForLog).RawData;
        }
        catch (Exception cex)
        {
            json = cex.Message;
        }
        _logger?.LogError(ex, "Input message processing error. TgMessage: {TgMessage}", json);
    }

    private MessageActionEventArgs CreateMessageActionEventArgs(TgMessage tgMessage, MessageAction messageAction)
    {
        return messageAction != MessageAction.Undefined
            ? new MessageActionEventArgs()
            {
                Message = _itemConverter.ToGeneralMessage(tgMessage),
                Chat = _itemConverter.ToGeneralChat(tgMessage.Chat),
                User = _itemConverter.ToGeneralUser(tgMessage.From),
                ChatType = _itemConverter.ToGeneralChatType(tgMessage.Chat.Type),
                Action = messageAction
            }
            : new MessageActionEventArgs();
    }


    /// <summary> Tries to find the same item in the database and set it's Id </summary>
    /// <param name="user"></param>
    /// <returns><see langword="true"/> if the <see cref="user"/> found in the database, otherwise false</returns>
    private async Task<bool> TrySetExistingUserIdAndRole(User user)
    {
        if (user is null)
            throw new ArgumentNullException(nameof(user));

        var rawData = JsonSerializer.Deserialize<JsonNode>(user.RawData);
        if (rawData["Id"] != null)
        {
            var tgUserId = (long)rawData["Id"];
            var existingUser = await _usersRepo.FindByRawDataIdAsync(tgUserId);

            user.Id = existingUser?.Id ?? 0;
            user.UserRoleId = existingUser?.UserRoleId ?? "USER";
            return true;
        }

        return false;
    }

    /// <summary> Tries find the same item in the database and set it's Id  </summary>
    /// <param name="chat"></param>
    /// <returns><see langword="true"/> if the <see cref="chat"/> found in the database, otherwise false</returns>
    private async Task<bool> TrySetExistingChatId(Chat chat)
    {
        if (chat is null)
            throw new ArgumentNullException(nameof(chat));

        var rawData = JsonSerializer.Deserialize<JsonNode>(chat.RawData);
        if (rawData["Id"] != null)
        {
            var tgChatId = (long)rawData["Id"];
            var existingChat = await _chatsRepo.FindByRawDataIdAsync(tgChatId);

            if (existingChat is not null)
            {
                chat.Id = existingChat.Id;
                return true;
            }
        }

        return false;
    }

    /// <summary> Tries find the same item in the database and set it's Id  </summary>
    /// <param name="message"></param>
    /// <returns><see langword="true"/> if the <see cref="message"/> found in the database, otherwise false</returns>
    private async Task<bool> TrySetExistingMessageId(Message message)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var rawData = JsonSerializer.Deserialize<JsonNode>(message.RawData);

        if (rawData["MessageId"] != null && rawData["Chat"]?["Id"] != null)
        {
            var tgMessageId = (int)rawData["MessageId"];
            var tgChatId = (long)rawData["Chat"]["Id"];
            var existingMessage = await _messagesRepo.FindByRawDataIdsAsync(tgMessageId, tgChatId);

            if (existingMessage is not null)
            {
                message.Id = existingMessage.Id;
                return true;
            }
        }
        return false;
    }
}

