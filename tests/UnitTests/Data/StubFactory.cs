using System;
using Zs.Bot.Data.Models;
using TelegramChat = Telegram.Bot.Types.Chat;
using TelegramChatType = Telegram.Bot.Types.Enums.ChatType;
using TelegramMessage = Telegram.Bot.Types.Message;
using TelegramUser = Telegram.Bot.Types.User;

namespace UnitTests.Data;

internal static class StubFactory
{
    internal static Chat CreateChat(int chatId = 0)
    {
        chatId = PrepareId(chatId);

        return new Chat
        {
            Id = chatId,
            Name = $"testChatName_{chatId}",
            Description = "testChatDescription",
            ChatTypeId = "testTypeId",
            RawData = @"{
                            ""AllMembersAreAdministrators"": false,
                            ""FirstName"": ""Сергей"",
                            ""Id"": 210281448,
                            ""LastName"": ""Зуев"",
                            ""Type"": 0,
                            ""Username"": ""zuev56""
                        }",
            RawDataHash = $"rawDataHash_{chatId}"
        };
    }

    private static int PrepareId(int id)
        => id != 0 ? id : Random.Shared.Next(1, 9999);

    internal static Chat[] CreateChats(int amount)
    {
        var chats = new Chat[amount];

        for (var i = 0; i < amount; i++)
        {
            chats[i] = CreateChat(i + 1);
        }

        return chats;
    }

    internal static User CreateUser(int userId = 0)
    {
        userId = PrepareId(userId);

        return new User
        {
            Id = userId,
            Name = $"testUserName_{userId}",
            UserRoleId = "TestRole",
            RawData = @"{
                            ""FirstName"": ""Сергей"",
                            ""Id"": 210281448,
                            ""IsBot"": false,
                            ""LanguageCode"": ""ru"",
                            ""LastName"": ""Зуев"",
                            ""Username"": ""zuev56""
                        }",
            RawDataHash = $"rawDataHash_{userId}"
        };
    }

    internal static User[] CreateUsers(int amount)
    {
        var users = new User[amount];

        for (var i = 0; i < amount; i++)
        {
            users[i] = CreateUser(i + 1);
        }

        return users;
    }

    internal static Message CreateMessage(int chatId, int messageId = 0)
    {
        messageId = PrepareId(messageId);

        return new Message
        {
            Id = messageId,
            ChatId = chatId,
            Text = $"testText_{messageId}",
            MessageTypeId = "testMessageTypeId",
            MessengerId = "testMessagerId",
            RawData = @"{
                            ""Test"": ""Test""
                        }",
            RawDataHash = $"rawDataHash_{chatId}_{messageId}"
        };
    }

    internal static Message[] CreateMessages(int chatId, int amount)
    {
        var messages = new Message[amount];

        for (var i = 0; i < amount; i++)
        {
            messages[i] = CreateMessage(chatId, i + 1);
        }

        return messages;

    }

    internal static TelegramMessage CreateTelegramMessage(string messageText, int messageId = 0, int chatId = 0, int userId = 0)
    {
        messageId = PrepareId(messageId);
        chatId = PrepareId(chatId);
        userId = PrepareId(userId);

        return new TelegramMessage
        {
            MessageId = messageId,
            Chat = CreateTelegramChat(chatId),
            From = CreateTelegramUser(userId),
            Date = DateTime.Now,
            Text = messageText
        };
    }

    internal static TelegramChat CreateTelegramChat(int chatId = 0)
    {
        chatId = PrepareId(chatId);

        return new TelegramChat
        {
            Id = chatId,
            FirstName = $"stubChatFirstName_{chatId}",
            LastName = $"stubChatLastName_{chatId}",
            Username = $"stubChatUsername_{chatId}",
            Type = TelegramChatType.Private
        };
    }

    internal static TelegramUser CreateTelegramUser(int userId = 0)
    {
        userId = PrepareId(userId);

        return new TelegramUser
        {
            Id = userId,
            IsBot = false,
            FirstName = $"stubUserFirstName_{userId}",
            Username = $"stubUserUsername_{userId}"
        };
    }

    internal static TelegramUser CreateTelegramBot(int userId = 0)
    {
        userId = PrepareId(userId);

        return new TelegramUser
        {
            Id = userId,
            IsBot = true,
            FirstName = $"stubBotFirstName_{userId}",
            Username = $"stubBot_{userId}"
        };
    }
}