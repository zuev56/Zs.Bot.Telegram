using Telegram.Bot.Types.Enums;
using UnitTests.Data;
using Xunit;
using Zs.Bot.Messenger.Telegram;
using Zs.Bot.Services.Messaging;
using Zs.Common.Extensions;

namespace UnitTests;

public sealed class ItemConverterTests
{
    [Theory]
    [InlineData("Simple message text")]
    [InlineData(null)]
    [InlineData("Message text with Unicode: \uD83D\uDE4C\uD83C\uDFFB")]
    public void ConvertToGeneralMessage(string messageText)
    {
        var rawTelegramMessage = StubFactory.CreateTelegramMessage(messageText);
        var preparedTelegramMessage = new TgMessage(rawTelegramMessage)
        {
            IsSucceed = true,
            SendingFails = 2,
            FailDescription = "testFailDescription"
        };

        var itemConverter = CreateItemConverter();
        var generalMessage = itemConverter.ToGeneralMessage(preparedTelegramMessage);

        Assert.Equal(preparedTelegramMessage.Text.ReplaceEmojiWithX(), generalMessage.Text);
        Assert.Equal(preparedTelegramMessage.IsSucceed, generalMessage.IsSucceed);
        Assert.Equal(preparedTelegramMessage.SendingFails, generalMessage.FailsCount);
        Assert.Equal(preparedTelegramMessage.FailDescription, generalMessage.FailDescription);
    }

    private static IToGeneralItemConverter CreateItemConverter() => new ItemConverter();


    [Theory]
    [InlineData(ChatType.Private, Zs.Bot.Data.Enums.ChatType.Private)]
    [InlineData(ChatType.Group, Zs.Bot.Data.Enums.ChatType.Group)]
    [InlineData(ChatType.Channel, Zs.Bot.Data.Enums.ChatType.Channel)]
    [InlineData(ChatType.Supergroup, Zs.Bot.Data.Enums.ChatType.Group)]
    public void ConvertToGeneralChatType(
        ChatType telegramChatType,
        Zs.Bot.Data.Enums.ChatType expectedZsBotChatType)
    {
        var itemConverter = CreateItemConverter();
        var actualZsBotChatType = itemConverter.ToGeneralChatType(telegramChatType);

        Assert.Equal(expectedZsBotChatType, actualZsBotChatType);
    }

    [Fact]
    public void ConvertToGeneralChat()
    {
        var chat = StubFactory.CreateTelegramChat();

        var itemConverter = CreateItemConverter();
        var generalChat = itemConverter.ToGeneralChat(chat);

        Assert.Equal(chat.Description, generalChat.Description);
        Assert.Equal(chat.Username, generalChat.Name);
    }

    [Fact]
    public void ConvertToGeneralUser()
    {
        var user = StubFactory.CreateTelegramUser();

        var itemConverter = CreateItemConverter();
        var generalUser = itemConverter.ToGeneralUser(user);

        Assert.Equal(user.IsBot, generalUser.IsBot);
        Assert.Equal(user.Username, generalUser.Name);
        Assert.Equal($"{user.FirstName} {user.LastName}".Trim(), generalUser.FullName);
    }
}