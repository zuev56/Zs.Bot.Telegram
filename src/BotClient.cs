using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Zs.Bot.Data.Models;
using Zs.Bot.Services.Messaging;
using Zs.Bot.Services.Pipeline;
using Zs.Bot.Telegram.Extensions;
using Zs.Common.Extensions;
using DbChat = Zs.Bot.Data.Models.Chat;
using DbMessage = Zs.Bot.Data.Models.Message;
using Message = Telegram.Bot.Types.Message;

namespace Zs.Bot.Telegram;

public sealed class BotClient : IBotClient
{
    private const int MaxMessageLength = 4096;
    private PipelineStep? _firstMessagePipelineStep;
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly ILogger _logger;

    public BotClient(ITelegramBotClient telegramBotClient, ILogger<BotClient> logger)
    {
        _telegramBotClient = telegramBotClient;
        _logger = logger;

        InitializeTelegramBotClient();
    }

    private void InitializeTelegramBotClient()
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new [] { UpdateType.Message }
        };

        _telegramBotClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions
        );
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        switch (update.Type)
        {
            case UpdateType.Message:
                await StartMessagePipelineAsync(update.Message!, MessageAction.Received, cancellationToken);
                break;
            default:
                _logger.LogTraceIfNeed("TelegramBot other update ({UpdateType})", update.Type);
                break;
        }
    }

    private async Task StartMessagePipelineAsync(Message message, MessageAction messageAction, CancellationToken cancellationToken)
    {
        var eventArgs = CreateEventArgs(message, messageAction);

        try
        {
            if (_firstMessagePipelineStep != null)
                await _firstMessagePipelineStep.PerformAsync(eventArgs, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogErrorIfNeed(e, "Message pipeline failed");
        }
    }

    public void AddToMessagePipeline(PipelineStep nextStep, CancellationToken cancellationToken)
    {
        if (_firstMessagePipelineStep == null)
        {
            _firstMessagePipelineStep = nextStep;
        }
        else
        {
            var lastStep = _firstMessagePipelineStep.GetLastStep();
            lastStep.Next = nextStep;
        }
    }

    private static MessageActionData CreateEventArgs(Message message, MessageAction messageAction)
    {
        var dbUser = message.From?.ToDbUser();
        var dbChat = message.Chat.ToDbChat();
        var dbMessage = message.ToDbMessage();

        return new MessageActionData
        {
            Message = dbMessage,
            Chat = dbChat,
            User = dbUser,
            Action = messageAction
        };
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogErrorIfNeed(exception, "Telegram.Bot.API error: {Message}", exception.Message);
        return Task.CompletedTask;
    }

    public async Task<DbMessage> SendMessageAsync(string text, DbChat chat, DbMessage? messageToReply, CancellationToken cancellationToken = default)
    {
        text = PrepareMessageText(text);
        var telegramChatId = chat.Id;
        var replyToMessageId = (int?)messageToReply?.Id;
        var message = await _telegramBotClient.SendTextMessageAsync(
            telegramChatId, text, replyToMessageId: replyToMessageId, cancellationToken: cancellationToken);

        await StartMessagePipelineAsync(message, MessageAction.Sent, cancellationToken);

        return message.ToDbMessage();
    }

    private static string PrepareMessageText(string text)
    {
        if (text.Length > MaxMessageLength)
            return text[..(MaxMessageLength - 3)] + "...";

        return text;
    }

    public async Task DeleteMessageAsync(DbMessage message, CancellationToken cancellationToken = default)
    {
        var chatId = message.ChatId;
        var messageId = (int)message.Id;
        await _telegramBotClient.DeleteMessageAsync(chatId, messageId, cancellationToken: cancellationToken);

        var telegramMessage = JsonSerializer.Deserialize<Message>(message.RawData)!;
        await StartMessagePipelineAsync(telegramMessage, MessageAction.Deleted, cancellationToken);
    }

    public async Task<string> GetBotInfoAsync(CancellationToken cancellationToken = default)
    {
        var bot = await _telegramBotClient.GetMeAsync(cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(bot);
        _logger.LogDebugIfNeed("Get bot info: {Bot}", json);

        return json;
    }
}