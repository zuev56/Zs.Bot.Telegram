using UnitTests.Data;
using Xunit;
using Zs.Bot.Messenger.Telegram;
using Zs.Bot.Services.Messaging;
using Zs.Common.Extensions;

namespace UnitTests
{
    public class ItemConverterTests
    {
        [Theory]
        [InlineData("Simple message text")]
        [InlineData(null)]
        [InlineData("Message text with Unicode: \uD83D\uDE4C\uD83C\uDFFB")]
        public void ConvertToGeneralMessage(string messageText)
        {
            // Arrange
            var rawTelegramMessage = StubFactory.CreateTelegramMessage(messageText);
            var preparedTelegramMessage = new TgMessage(rawTelegramMessage)
            {
                IsSucceed = true,
                SendingFails = 2,
                FailDescription = "testFailDescription"
            };

            // Act
            var itemConverter = CreateItemConverter();
            var generalMessage = itemConverter.ToGeneralMessage(preparedTelegramMessage);

            // Assert
            Assert.Equal(preparedTelegramMessage.Text.ReplaceEmojiWithX(), generalMessage?.Text);
            Assert.Equal(preparedTelegramMessage.IsSucceed, generalMessage?.IsSucceed);
            Assert.Equal(preparedTelegramMessage.SendingFails, generalMessage?.FailsCount);
            Assert.Equal(preparedTelegramMessage.FailDescription, generalMessage?.FailDescription);
        }

        public IToGenegalItemConverter CreateItemConverter() => new ItemConverter();


        [Theory]
        [InlineData(Telegram.Bot.Types.Enums.ChatType.Private, Zs.Bot.Data.Enums.ChatType.Private)]
        [InlineData(Telegram.Bot.Types.Enums.ChatType.Group, Zs.Bot.Data.Enums.ChatType.Group)]
        [InlineData(Telegram.Bot.Types.Enums.ChatType.Channel, Zs.Bot.Data.Enums.ChatType.Channel)]
        [InlineData(Telegram.Bot.Types.Enums.ChatType.Supergroup, Zs.Bot.Data.Enums.ChatType.Group)]
        public void ConvertToGeneralChatType(
            Telegram.Bot.Types.Enums.ChatType telegramChatType,
            Zs.Bot.Data.Enums.ChatType expectedZsBotChatType)
        {
            // Act
            var itemConverter = CreateItemConverter();
            var actualZsBotChatType = itemConverter.ToGeneralChatType(telegramChatType);

            // Assert
            Assert.Equal(expectedZsBotChatType, actualZsBotChatType);
        }

        [Fact]
        public void ConvertToGeneralChat()
        {
            // Arrange
            var chat = StubFactory.CreateTelegramChat();

            // Act
            var itemConverter = CreateItemConverter();
            var generalChat = itemConverter.ToGeneralChat(chat);

            // Assert
            Assert.Equal(chat.Description, generalChat.Description);
            Assert.Equal(chat.Username, generalChat.Name);
        }

        [Fact]
        public void ConvertToGeneralUser()
        {
            // Arrange
            var user = StubFactory.CreateTelegramUser();

            // Act
            var itemConverter = CreateItemConverter();
            var generalUser = itemConverter.ToGeneralUser(user);

            // Assert
            Assert.Equal(user.IsBot, generalUser.IsBot);
            Assert.Equal(user.Username, generalUser.Name);
            Assert.Equal($"{user.FirstName} {user.LastName}".Trim(), generalUser.FullName);
        }

    }
}
