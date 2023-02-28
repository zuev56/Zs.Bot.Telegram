using System;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Zs.Bot.Data.Abstractions;
using Zs.Bot.Data.Enums;
using Zs.Bot.Data.Models;
using Zs.Bot.Services.Messaging;
using Zs.Common.Extensions;
using Zs.Common.Models;
using TelegramChat = Telegram.Bot.Types.Chat;
using TelegramMessage = Telegram.Bot.Types.Message;
using TelegramMessageType = Telegram.Bot.Types.Enums.MessageType;
using TelegramUser = Telegram.Bot.Types.User;

namespace Zs.Bot.Messenger.Telegram;

internal sealed class OutputMessageProcessor : IOutputMessageProcessor
{
    private readonly IChatsRepository _chatsRepo;
    private readonly IUsersRepository _usersRepo;
    private readonly IMessagesRepository _messagesRepo;
    private readonly IToGenegalItemConverter _itemConverter = new ItemConverter();
    private readonly ILogger<OutputMessageProcessor> _logger;

    private readonly Buffer<TgMessage> _outputMessageBuffer = new();
    private readonly int _sendingRetryLimit = 5;

    public ITelegramBotClient BotClient { get; }

    public event EventHandler<MessageActionEventArgs> MessageProcessed;


    public OutputMessageProcessor(
        ITelegramBotClient telegramBotClient,
        IChatsRepository chatsRepo,
        IUsersRepository usersRepo,
        IMessagesRepository messagesRepo,
        ILogger<OutputMessageProcessor> logger = null
        )
    {
        BotClient = telegramBotClient ?? throw new ArgumentNullException(nameof(telegramBotClient));
        _chatsRepo = chatsRepo ?? throw new ArgumentNullException(nameof(chatsRepo));
        _usersRepo = usersRepo ?? throw new ArgumentNullException(nameof(usersRepo));
        _messagesRepo = messagesRepo ?? throw new ArgumentNullException(nameof(messagesRepo));

        _logger = logger;

        _outputMessageBuffer.OnEnqueue += OutputMessageBuffer_OnEnqueue;
    }

    public bool EnqueueMessage(Chat chat, string messageText, Message messageToReply = null)
    {
        try
        {
            if (chat is null)
                throw new ArgumentNullException(nameof(chat));

            if (string.IsNullOrWhiteSpace(messageText))
                throw new ArgumentNullException(nameof(messageText), "Message must have a body!");

            var tgChat = JsonSerializer.Deserialize<TelegramChat>(chat.RawData);
            var tgMessage = messageToReply is { }
                          ? JsonSerializer.Deserialize<TelegramMessage>(messageToReply.RawData)
                          : null;

            var msg = new TgMessage(tgChat, messageText)
            {
                ReplyToMessage = tgMessage
            };

            _outputMessageBuffer.Enqueue(msg);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Adding message to outbox error");
            return false;
        }
    }

    public bool EnqueueMessage(int chatId, string text)
    {
        var chat = _chatsRepo.FindByIdAsync(chatId).Result;
        return EnqueueMessage(chat, text);
    }

    public async Task<bool> EnqueueMessageAsync(string messageText, params Role[] userRoles)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(messageText))
                throw new ArgumentNullException(nameof(messageText), "Message must have a body!");

            if (userRoles == null || userRoles.Length == 0)
                throw new ArgumentException($"{userRoles} must have at least one element", nameof(userRoles));

            var dbUsers = await _usersRepo.FindByRoleIdsAsync(userRoles);

            var tgUsers = dbUsers.Select(u => JsonSerializer.Deserialize<TelegramUser>(u.RawData));

            var tgChats = (await _chatsRepo.FindAllAsync()).Select(c => JsonSerializer.Deserialize<TelegramChat>(c.RawData))
                .Where(c => c.Id is >= int.MinValue and <= int.MaxValue && tgUsers.Select(u => u.Id).Contains((int)c.Id)).ToList();

            foreach (var chat in tgChats)
                _outputMessageBuffer.Enqueue(new TgMessage(chat, messageText));

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Adding message to outbox error");
            return false;
        }
    }

    private void OutputMessageBuffer_OnEnqueue(object sender, TgMessage item)
    {
        Task task = null;
        task = Task.Run(() => ProcessOutputMessages(task));
    }


    private async Task ProcessOutputMessages(Task currentTask)
    {
        TgMessage msgForLog = null;
        try
        {
            while (_outputMessageBuffer.TryDequeue(out TgMessage tgMessage))
            {
                msgForLog = tgMessage;

                var sendingResult = await SendMessageFinallyAsync(tgMessage, currentTask);

                // TODO: Раньше было сравнение с Enum, а теперь стало не столь однозначно - продумать и переделать
                if (sendingResult.Successful)// && sendingResult.HasWarnings)
                    continue;

                // When an error occured during sending
                tgMessage.From ??= await BotClient.GetMeAsync();

                // TODO: Extract method
                var args = new MessageActionEventArgs
                {
                    Message = _itemConverter.ToGeneralMessage(tgMessage),
                    Chat = _itemConverter.ToGeneralChat(tgMessage.Chat),
                    User = _itemConverter.ToGeneralUser(tgMessage.From),
                    ChatType = _itemConverter.ToGeneralChatType(tgMessage.Chat.Type),
                    Action = MessageAction.Sending
                };

                await TrySetExistingUserIdAndRole(args.User).ConfigureAwait(false);
                await TrySetExistingChatId(args.Chat).ConfigureAwait(false);

                Volatile.Read(ref MessageProcessed)?.Invoke(this, args);

                msgForLog = null;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Output message processing error. Message: {Message}", msgForLog);
        }
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
            var tgUserId = (int)rawData["Id"];
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

    private async Task<Result> SendMessageFinallyAsync(TgMessage message, Task currentTask)
    {
        // Telegram.Bot.API не позволяет отправлять сообщения, 
        // содержащие текст вида */command@botName*
        try
        {
            TelegramMessage tgMessage = null;

            switch (message.Type)
            {
                case TelegramMessageType.Text:
                    if (string.IsNullOrWhiteSpace(message.Text))
                        throw new Exception("Text message have no text");

                    TelegramMessage tmp = message.ReplyToMessage is null
                            ? await BotClient.SendTextMessageAsync(
                                  message.Chat.Id,
                                  message.Text)
                            : await BotClient.SendTextMessageAsync(
                                  message.Chat.Id,
                                  message.Text,
                                  replyToMessageId: (int)message.ReplyToMessageId);
                    tgMessage = new TgMessage(tmp);
                    break;
                default:
                    await BotClient.SendTextMessageAsync(message.Chat.Id, $"Unable to send message type of {message.Type}");
                    break;
            }

            _logger?.LogInformation("Send message (TelegramChatId: {TelegramChatId}, TelegramMessageId: {TelegramMessageId}, Text: \"{MessageText}\")", tgMessage?.Chat.Id, tgMessage?.MessageId, tgMessage?.Text);

            if (tgMessage != null)
            {
                message.Parse(tgMessage);
                message.IsSucceed = true;
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            if (ex is ApiRequestException)
            {
                if (message.SendingFails > 1)
                {
                    message.IsSucceed = false;
                    ex.Data.Add("Message", message);
                    _logger?.LogError(ex, "Message sending error (Message: {message})", message);
                    return Result.Fail(Fault.Unknown.SetMessage("Message sending error"));
                }

                currentTask.Wait(3000);
            }

            if (message.SendingFails < _sendingRetryLimit)
            {
                _logger?.LogWarning(ex, "Message sending error. Retry... (Message: {message})", message);
                var jsonSerializerOptions = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                };
                message.FailDescription = JsonSerializer.Serialize(ex, jsonSerializerOptions);
                message.SendingFails++;
                currentTask.Wait(2000 * message.SendingFails);
                _outputMessageBuffer.Enqueue(message);
                return Result.Fail(Fault.Unknown.SetMessage("Message sending error. Retry..."));
            }

            try
            {
                message.IsSucceed = false;
                ex.Data.Add("Message", message);
                _logger?.LogError(ex, "Message sending error");
                return Result.Fail(Fault.Unknown.SetMessage("Message sending error"));
            }
            catch { return Result.Fail(Fault.Unknown.SetMessage("Message sending error")); }
        }
    }

    public async Task<bool> DeleteMessageAsync(Message dbMessage)
    {
        if (dbMessage is null)
            throw new ArgumentNullException(nameof(dbMessage));

        var dbChat = await _chatsRepo.FindByIdAsync(dbMessage.ChatId);
        if (dbChat == null)
        {
            _logger?.LogError("Message deleting error. Chat not found in database (Chat: {DbChat}, Message: {DbMessage}, User: {DbUser})", dbChat, dbMessage, dbMessage?.User);
            return false;
        }

        var deleteResult = await DeleteMessageAsync(dbChat, dbMessage);

        return deleteResult.Successful;
    }

    private async Task<Result> DeleteMessageAsync(Chat chat, Message message)
    {
        TelegramChat tgChat = null;
        TelegramMessage tgMessage = null;
        try
        {
            if (chat == null)
                throw new ArgumentNullException(nameof(chat));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            tgChat = JsonSerializer.Deserialize<TelegramChat>(chat.RawData);
            tgMessage = JsonSerializer.Deserialize<TelegramMessage>(message.RawData);

            message.IsDeleted = await TryDeleteMessageAsync(tgChat, tgMessage).ConfigureAwait(false);

            var args = new MessageActionEventArgs()
            {
                Chat = chat,
                Message = message,
                ChatType = _itemConverter.ToGeneralChatType(tgChat.Type),
                Action = MessageAction.Deleted
            };

            Volatile.Read(ref MessageProcessed)?.Invoke(this, args);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Message deleting error (TelegramChatId: {TelegramChatId}, TelegramMessageId: {TelegramMessageId})", tgChat?.Id, tgMessage?.MessageId);
            return Result.Fail(Fault.Unknown.SetMessage("Message deleting error"));
        }
    }

    private async Task<bool> TryDeleteMessageAsync(TelegramChat tgChat, TelegramMessage tgMessage)
    {
        try
        {
            await BotClient.DeleteMessageAsync(tgChat.Id, tgMessage.MessageId);

            _logger?.LogInformation("Delete message (TelegramChatId: {TelegramChatId}, TelegramMessageId: {TelegramMessageId})", tgChat.Id, tgMessage.MessageId);
            return true;
        }
        catch (ApiRequestException arex)
        {
            // E.g. if the message is not found in Telegram
            _logger?.LogError(arex, "Message deleting API error (TelegramChatId: {TelegramChatId}, TelegramMessageId: {TelegramMessageId})", tgChat?.Id, tgMessage?.MessageId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Message deleting error (TelegramChatId: {TelegramChatId}, TelegramMessageId: {TelegramMessageId})", tgChat?.Id, tgMessage?.MessageId);
            return false;
        }
    }
}