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
using Telegram.Bot.Types.Enums;
using Zs.Bot.Data.Abstractions;
using Zs.Bot.Data.Enums;
using Zs.Bot.Services.Commands;
using Zs.Bot.Services.DataSavers;
using Zs.Bot.Services.Messaging;
using Zs.Common.Extensions;
using Zs.Common.Models;

namespace Zs.Bot.Messenger.Telegram;

public sealed class TelegramMessenger : IMessenger
{
    // TODO: получение информации о боте и удаление сообщений не совсем уместны в сервисах IMessenger и IOutputMessageProcessor
    private readonly IInputMessageProcessor _inputMessageProcessor;
    private readonly IOutputMessageProcessor _outputMessageProcessor;
    private readonly ICommandManager _commandManager;
    private readonly IMessageDataSaver _messageDataSaver;
    private readonly ILogger<TelegramMessenger> _logger;
    private string _botName;
    private readonly List<DateTime> _requestTimeOutExceptionDates = new (100);
    private readonly List<DateTime> _makingRequestExceptionDates = new(100);
    private readonly List<ErrorInfo> _errors = new(500);

    public event EventHandler<MessageActionEventArgs> MessageReceived;
    public event EventHandler<MessageActionEventArgs> MessageEdited;
    public event EventHandler<MessageActionEventArgs> MessageSent;
    public event EventHandler<MessageActionEventArgs> MessageDeleted;

    public TelegramMessenger(
        ITelegramBotClient telegramBotClient,
        IChatsRepository chatsRepo,
        IUsersRepository usersRepo,
        IMessagesRepository messagesRepo,
        IMessageDataSaver messageDataSaver = null,
        ICommandManager commandManager = null,
        ILoggerFactory loggerfFactory = null)
    {

        telegramBotClient.OnApiResponseReceived += BotClient_OnApiResponseReceived;
        telegramBotClient.OnMakingApiRequest += BotClient_OnMakingApiRequest;

        telegramBotClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync
        );

        _inputMessageProcessor = new InputMessageProcessor(chatsRepo, usersRepo, messagesRepo, loggerfFactory?.CreateLogger<InputMessageProcessor>());
        _inputMessageProcessor.MessageProcessed += InputMessageProcessor_MessageProcessed;

        _outputMessageProcessor = new OutputMessageProcessor(telegramBotClient, chatsRepo, usersRepo, messagesRepo, loggerfFactory?.CreateLogger<OutputMessageProcessor>());
        _outputMessageProcessor.MessageProcessed += OutputMessageProcessor_MessageProcessed;

        _messageDataSaver = messageDataSaver;

        if (commandManager != null)
        {
            _commandManager = commandManager;
            _commandManager.CommandCompleted += CommandManager_CommandCompleted;
        }

        _logger = loggerfFactory?.CreateLogger<TelegramMessenger>();

    }

    #region Обработчики событий TelegramBotClient

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, global::Telegram.Bot.Types.Update update, CancellationToken cancellationToken)
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
                    var enqueueIsSuccess = _inputMessageProcessor.EnqueueMessage(new TgMessage(update.Message), out var messageActionEventArgs);
                    // TODO: Add check
                    break;
                }
            case UpdateType.EditedMessage:
                {
                    // TODO: Extract method
                    _logger?.LogInformationIfNeed("Edit message (TelegramChatId: {TelegramChatId}, TelegramMessageId: {TelegramMessageId}, Text: {MessageText})", update.EditedMessage.Chat.Id, update.EditedMessage.MessageId, update.EditedMessage.Text);
                    var enqueueIsSuccess = _inputMessageProcessor.EnqueueMessage(new TgMessage(update.EditedMessage), out var messageActionEventArgs);
                    // TODO: Add check
                    break;
                }
            default:
                _logger?.LogTrace("TelegramBot other update ({UpdateType})", update.Type);
                break;
        }
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

        _logger?.LogError(exception, "Telegram.Bot.API error: {Message}", exception.Message);
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
            _logger?.LogError("Telegram.Bot.API '{ExceptionMessage}' was {Amount} times in {TimeIntervalInMinutes} minutes",
                exceptionMessage, exceptionDates.Count, (int)(seconds / 60));

            exceptionDates.Clear();
            exceptionDates.TrimExcess();
            exceptionDates.EnsureCapacity(100);
        }
    }

    internal void HandleRequestExceptionNew(RequestException exception, TimeSpan collectionPeriod)
    {
        // TODO: Use afret test
        _errors.Add(new(exception.Message));

        var sameErrors = _errors.Where(e => e.Text == exception.Message).OrderBy(e => e.DateTime);
        var sameErrorsCount = sameErrors.Count();

        if (sameErrorsCount == 1)
        {
            _logger?.LogError("Telegram.Bot.API '{ErrorMessage}'", exception.Message);
            return;
        }

        var period = sameErrors.Last().DateTime - sameErrors.ElementAt(1).DateTime;

        if (period > collectionPeriod)
        {
            _logger?.LogError("Telegram.Bot.API '{ErrorMessage}' was {Amount} times in {TimeIntervalInMinutes} minutes",
                exception.Message, sameErrorsCount, period.ToString("HH:mm:ss"));

            _errors.RemoveAll(e => e.Text == exception.Message);
            _errors.TrimExcess();
            _errors.EnsureCapacity(500);
        }
    }

    private ValueTask BotClient_OnApiResponseReceived(ITelegramBotClient botClient, ApiResponseEventArgs e, CancellationToken cancellationToken = default)
    {
        _logger?.LogTrace("ApiResponseReceived", e);
        return ValueTask.CompletedTask;
    }

    private ValueTask BotClient_OnMakingApiRequest(ITelegramBotClient botClient, ApiRequestEventArgs e, CancellationToken cancellationToken = default)
    {
        _logger?.LogTrace("MakingApiRequest", e);
        return ValueTask.CompletedTask;
    }

    #endregion

    private async void InputMessageProcessor_MessageProcessed(object sender, MessageActionEventArgs e)
    {
        switch (e.Action)
        {
            case MessageAction.Received:
                if (_messageDataSaver is not null)
                    await _messageDataSaver.SaveNewMessageData(e).ConfigureAwait(false);

                Volatile.Read(ref MessageReceived)?.Invoke(this, e);
                if (!e.IsHandled)
                {
                    if (_commandManager is not null
                        && BotCommand.IsCommand(e.Message.Text, _botName)
                        && !await _commandManager.TryEnqueueCommandAsync(e.Message))
                    {
                        _outputMessageProcessor.EnqueueMessage(e.Chat, $"Unknown command '{e.Message.Text}'");
                    }
                    //else if (message is File)
                }
                break;
            case MessageAction.Edited:
                if (_messageDataSaver is not null)
                    await _messageDataSaver.EditSavedMessage(e).ConfigureAwait(false);

                Volatile.Read(ref MessageEdited)?.Invoke(this, e);
                break;
        }
    }

    private async void OutputMessageProcessor_MessageProcessed(object sender, MessageActionEventArgs e)
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


    private void CommandManager_CommandCompleted(object sender, CommandResult result)
    {
        _outputMessageProcessor.EnqueueMessage(result.ChatIdForAnswer, result.Text);
    }

    public bool AddMessageToOutbox(Data.Models.Chat chat, string messageText, Data.Models.Message messageToReply = null)
    {
        // TODO: Remove try\catch and change tests
        try
        {
            if (chat is null)
                throw new ArgumentNullException(nameof(chat));

            if (string.IsNullOrWhiteSpace(messageText))
                throw new ArgumentNullException(nameof(messageText));

            return _outputMessageProcessor.EnqueueMessage(chat, messageText, messageToReply);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Adding a message to outbox error. ChatId = {ChatId}, MessageText = {MessageText}", chat?.Id, messageText);
            return false;
        }
    }

    public async Task<bool> AddMessageToOutboxAsync(string messageText, params Role[] userRoles)
    {
        // TODO: Remove try\catch and change tests
        try
        {
            if (string.IsNullOrWhiteSpace(messageText))
                throw new ArgumentNullException(nameof(messageText));

            if (userRoles is null || userRoles.Length == 0)
                throw new ArgumentNullException(nameof(userRoles));

            return await _outputMessageProcessor.EnqueueMessageAsync(messageText, userRoles).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Adding a message to outbox error. MessageText = {MessageText}, UserRoles = {UserRoles}", messageText, userRoles);
            return false;
        }
    }

    public async Task<bool> DeleteMessageAsync(Data.Models.Message message)
    {
        // TODO: Remove try\catch and change tests
        try
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return await _outputMessageProcessor.DeleteMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Message deleting error. MessageId = {MessageId}", message?.Id);
            return false;
        }
    }

    public async Task<JsonElement> GetBotInfoAsync(CancellationToken cancellationToken = default)
    {
        var bot = await _outputMessageProcessor.BotClient.GetMeAsync(cancellationToken);

        _logger?.LogInformation("Get bot info: {Bot}", bot);

        string json = JsonSerializer.Serialize(bot);

        var botInfo = JsonSerializer.Deserialize<JsonElement>(json);
        if (_botName == null && botInfo.ValueKind != JsonValueKind.Null)
            _botName = botInfo.EnumerateObject().FirstOrDefault(i => i.Name == "Username").Value.ToString();

        return botInfo;
    }

}

