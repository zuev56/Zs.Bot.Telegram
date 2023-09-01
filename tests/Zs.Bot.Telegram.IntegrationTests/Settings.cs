using System.ComponentModel.DataAnnotations;

namespace Zs.Bot.Telegram.IntegrationTests;

public sealed class Settings
{
    [Required]
    public string Token { get; set; } = null!;
    [Required]
    public long TelegramTestGroupChatId { get; set; }
}