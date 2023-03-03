using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Zs.Bot.Data.Abstractions;
using Zs.Bot.Data.Enums;
using Zs.Bot.Services.Commands;
using Zs.Bot.Services.DataSavers;
using Zs.Bot.Services.Messaging;
using Zs.Common.Extensions;
using BotCommand = Zs.Bot.Services.Commands.BotCommand;
using Chat = Zs.Bot.Data.Models.Chat;
using Message = Zs.Bot.Data.Models.Message;

namespace Zs.Bot.Messenger.Telegram;

public sealed class TelegramMessenger : IMessenger
{
    // TODO: получение информации о боте и удаление сообщений не совсем уместны в сервисах IMessenger и IOutputMessageProcessor
    private readonly IInputMessageProcessor _inputMessageProcessor;
    private readonly IOutputMessageProcessor _outputMessageProcessor;
    private readonly ICommandManager? _commandManager;
    private readonly IMessageDataSaver? _messageDataSaver;
    private readonly ILogger<TelegramMessenger>? _logger;
    private string? _botName;
    private readonly List<DateTime> _requestTimeOutExceptionDates = new ();
    private readonly List<DateTime> _makingRequestExceptionDates = new();

    public event EventHandler<MessageActionEventArgs>? MessageReceived;
    public event EventHandler<MessageActionEventArgs>? MessageEdited;
    public event EventHandler<MessageActionEventArgs>? MessageSent;
    public event EventHandler<MessageActionEventArgs>? MessageDeleted;

    public TelegramMessenger(
        ITelegramBotClient telegramBotClient,
        IChatsRepository chatsRepo,
        IUsersRepository usersRepo,
        IMessagesRepository messagesRepo,
        IMessageDataSaver? messageDataSaver = null,
        ICommandManager? commandManager = null,
        ILoggerFactory? loggerFactory = null)
    {

        telegramBotClient.OnApiResponseReceived += BotClient_OnApiResponseReceived;
        telegramBotClient.OnMakingApiRequest += BotClient_OnMakingApiRequest;

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };
        telegramBotClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: CancellationToken.None
        );

        _inputMessageProcessor = new InputMessageProcessor(chatsRepo, usersRepo, messagesRepo, loggerFactory?.CreateLogger<InputMessageProcessor>());
        _inputMessageProcessor.MessageProcessed += InputMessageProcessor_MessageProcessed;

        _outputMessageProcessor = new OutputMessageProcessor(telegramBotClient, chatsRepo, usersRepo, loggerFactory?.CreateLogger<OutputMessageProcessor>());
        _outputMessageProcessor.MessageProcessed += OutputMessageProcessor_MessageProcessed;

        _messageDataSaver = messageDataSaver;

        if (commandManager != null)
        {
            _commandManager = commandManager;
            _commandManager.CommandCompleted += CommandManager_CommandCompleted;
        }

        _logger = loggerFactory?.CreateLogger<TelegramMessenger>();

    }

    #region Обработчики событий TelegramBotClient

    private Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // TODO: Проверить
        // - Новое сообщение
        // - Изменение сообщения
        // - Удаление сообщения
        // - Команда от бота
        switch (update.Type)
        {
            case UpdateType.Message:
                {
                    // TODO: Extract method
                    _logger?.LogInformationIfNeed("Receive message (TelegramChatId: {TelegramChatId}, TelegramMessageId: {TelegramMessageId}, Text: {MessageText})", update.Message.Chat.Id, update.Message.MessageId, update.Message.Text);
                    _inputMessageProcessor.EnqueueMessage(new TgMessage(update.Message), out var messageActionEventArgs);
                    break;
                }
            case UpdateType.EditedMessage:
                {
                    // TODO: Extract method
                    _logger?.LogInformationIfNeed("Edit message (TelegramChatId: {TelegramChatId}, TelegramMessageId: {TelegramMessageId}, Text: {MessageText})", update.EditedMessage.Chat.Id, update.EditedMessage.MessageId, update.EditedMessage.Text);
                    _inputMessageProcessor.EnqueueMessage(new TgMessage(update.EditedMessage), out var messageActionEventArgs);
                    // TODO: Add check
                    break;
                }
            default:
                _logger?.LogTraceIfNeed("TelegramBot other update ({UpdateType})", update.Type);
                break;
        }

        return Task.CompletedTask;
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is RequestException rex)
        {
            // TODO: Try using inner exceptions instead of the Message
            if (rex.Message == "Request timed out")
            {
                HandleRequestException(_requestTimeOutExceptionDates, rex.Message);
                return Task.CompletedTask;
            }
            if (rex.Message == "Exception during making request")
            {
                HandleRequestException(_makingRequestExceptionDates, rex.Message);
                return Task.CompletedTask;
            }
            // New universal handler
            //HandleRequestExceptionNew(rex, 600);
        }

        _logger?.LogErrorIfNeed(exception, "Telegram.Bot.API error: {Message}", exception.Message);
        return Task.CompletedTask;
    }

    private void HandleRequestException(List<DateTime> exceptionDates, string exceptionMessage)
    {
        const int seconds = 600;
        const int limit = 20;

        exceptionDates.Add(DateTime.UtcNow);

        if (exceptionDates.Count > limit
            && exceptionDates.Last() - exceptionDates.First() > TimeSpan.FromSeconds(seconds))
        {
            _logger?.LogErrorIfNeed("Telegram.Bot.API '{ExceptionMessage}' was {Amount} times in {TimeIntervalInMinutes} minutes",
                exceptionMessage, exceptionDates.Count, (seconds / 60));

            exceptionDates.Clear();
            exceptionDates.TrimExcess();
            exceptionDates.EnsureCapacity(100);
        }
    }

    private ValueTask BotClient_OnApiResponseReceived(ITelegramBotClient botClient, ApiResponseEventArgs e, CancellationToken cancellationToken = default)
    {
        _logger?.LogTraceIfNeed("ApiResponseReceived", e);
        return ValueTask.CompletedTask;
    }

    private ValueTask BotClient_OnMakingApiRequest(ITelegramBotClient botClient, ApiRequestEventArgs e, CancellationToken cancellationToken = default)
    {
        _logger?.LogTraceIfNeed("MakingApiRequest", e);
        return ValueTask.CompletedTask;
    }

    #endregion

    private async void InputMessageProcessor_MessageProcessed(object? sender, MessageActionEventArgs e)
    {
        switch (e.Action)
        {
            case MessageAction.Received:
                if (_messageDataSaver is not null)
                {
                    await _messageDataSaver.SaveNewMessageData(e).ConfigureAwait(false);
                }

                Volatile.Read(ref MessageReceived)?.Invoke(this, e);
                if (!e.IsHandled)
                {
                    if (_commandManager is not null && e.Message.Text is not null
                        && BotCommand.IsCommand(e.Message.Text, _botName)
                        && !await _commandManager.TryEnqueueCommandAsync(e.Message).ConfigureAwait(false))
                    {
                        _outputMessageProcessor.EnqueueMessage(e.Chat, $"Unknown command '{e.Message.Text}'");
                    }
                    //else if (message is File)
                }
                break;
            case MessageAction.Edited:
                if (_messageDataSaver is not null)
                {
                    await _messageDataSaver.EditSavedMessage(e).ConfigureAwait(false);
                }

                Volatile.Read(ref MessageEdited)?.Invoke(this, e);
                break;
        }
    }

    private async void OutputMessageProcessor_MessageProcessed(object? sender, MessageActionEventArgs e)
    {
        if (_messageDataSaver is not null)
            await _messageDataSaver.SaveNewMessageData(e).ConfigureAwait(false);

        switch (e.Action)
        {
            case MessageAction.Sending:
                Volatile.Read(ref MessageSent)?.Invoke(this, e);
                break;
            case MessageAction.Deleted:
                Volatile.Read(ref MessageDeleted)?.Invoke(this, e);
                break;
        }
    }


    private void CommandManager_CommandCompleted(object? sender, CommandResult result)
    {
        _outputMessageProcessor.EnqueueMessage(result.ChatIdForAnswer, result.Text);
    }

    public bool AddMessageToOutbox(Chat chat, string messageText, Message? messageToReply = null)
    {
        // TODO: Remove try\catch and change tests
        try
        {
            ArgumentNullException.ThrowIfNull(chat);

            if (string.IsNullOrWhiteSpace(messageText))
            {
                throw new ArgumentNullException(nameof(messageText));
            }

            _outputMessageProcessor.EnqueueMessage(chat, messageText, messageToReply);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogErrorIfNeed(ex, "Adding a message to outbox error. ChatId = {ChatId}, MessageText = {MessageText}", chat?.Id, messageText);
            return false;
        }
    }

    public async Task<bool> AddMessageToOutboxAsync(string messageText, params Role[] userRoles)
    {
        // TODO: Remove try\catch and change tests
        try
        {
            if (string.IsNullOrWhiteSpace(messageText))
            {
                throw new ArgumentNullException(nameof(messageText));
            }

            if (userRoles is null || userRoles.Length == 0)
            {
                throw new ArgumentNullException(nameof(userRoles));
            }

            await _outputMessageProcessor.EnqueueMessageAsync(messageText, userRoles).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogErrorIfNeed(ex, "Adding a message to outbox error. MessageText = {MessageText}, UserRoles = {UserRoles}", messageText, userRoles);
            return false;
        }
    }

    public async Task<bool> DeleteMessageAsync(Message message)
    {
        // TODO: Remove try\catch and change tests
        try
        {
            ArgumentNullException.ThrowIfNull(message);

            return await _outputMessageProcessor.DeleteMessageAsync(message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogErrorIfNeed(ex, "Message deleting error. MessageId = {MessageId}", message.Id);
            return false;
        }
    }

    public async Task<JsonElement> GetBotInfoAsync(CancellationToken cancellationToken = default)
    {
        var bot = await _outputMessageProcessor.BotClient.GetMeAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformationIfNeed("Get bot info: {Bot}", bot);

        var json = JsonSerializer.Serialize(bot);

        var botInfo = JsonSerializer.Deserialize<JsonElement>(json);
        if (_botName == null && botInfo.ValueKind != JsonValueKind.Null)
        {
            _botName = botInfo.EnumerateObject().FirstOrDefault(static i => i.Name == "Username").Value.ToString();
        }

        return botInfo;
    }

}