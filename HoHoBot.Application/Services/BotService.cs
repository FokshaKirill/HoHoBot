using HoHoBot.Domain.Entities;
using HoHoBot.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Chat = HoHoBot.Domain.Entities.Chat;

namespace HoHoBot.Application.Services;

public class BotService
{
    private readonly ITelegramBotClient _botClient;
    private readonly HoHoBotDbContext _dbContext;

    public BotService(ITelegramBotClient botClient, HoHoBotDbContext dbContext)
    {
        _botClient = botClient;
        _dbContext = dbContext;
        SetBotCommands().Wait(); 
    }

    public async Task HandleUpdateAsync(Update update)
    {
        try
        {
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                var chatId = update.Message.Chat.Id;
                var chatType = update.Message.Chat.Type.ToString().ToLower();
                var userId = update.Message.From.Id;
                var username = update.Message.From.Username ?? "пользователь";
                var fullname = update.Message.From.FirstName + " " + update.Message.From.LastName ?? "Дим Димыч";

                // Удаляем упоминание бота из команды
                var messageText = update.Message.Text.Split(' ')[0].ToLower();
                
                if (!messageText.StartsWith("/"))
                    return;
                
                messageText = messageText.Contains('@') ? messageText.Split('@')[0] : messageText;

                switch (messageText)
                {
                    case "/start":
                        await HandleStartCommand(chatId, chatType, userId);
                        break;

                    case "/stop":
                        await HandleStopCommand(chatId, chatType, userId);
                        break;

                    case "/reset":
                        await HandleResetCommand(chatId, chatType, userId);
                        break;

                    case "/join":
                        await HandleJoinCommand(chatId, userId, username, fullname);
                        break;
                    
                    case "/info":
                        await ShowAllParticipants(chatId, chatType);
                        break;

                    default:
                        await _botClient.SendTextMessageAsync(chatId, "❓ Неизвестная команда. Попробуйте /start, /stop, /reset или /join.");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при сохранении чата: {ex}");
            await _botClient.SendTextMessageAsync(update.Message.Chat.Id, "❌ Произошла ошибка при регистрации чата.");
        }
    }

    private async Task<bool> IsUserAdmin(long chatId, long userId)
    {
        try
        {
            var admins = await _botClient.GetChatAdministratorsAsync(chatId);
            return admins.Any(a => a.User.Id == userId);
        }
        catch
        {
            return false;
        }
    }

    private async Task HandleStartCommand(long chatId, string chatType, long userId)
    {
        if (!await ValidateGroupAndAdmin(chatId, chatType, userId))
            return;

        var chatExists = await _dbContext.Chats.AnyAsync(c => c.ChatId == chatId);
        if (!chatExists)
        {
            _dbContext.Chats.Add(new Chat
            {
                ChatId = chatId,
                Name = chatId.ToString(),
                ChatType = chatType
            });
            await _dbContext.SaveChangesAsync();
            await _botClient.SendTextMessageAsync(chatId, "✅ Чат зарегистрирован и регистрация участников начата!");
        }
        else
        {
            await _botClient.SendTextMessageAsync(chatId, "⚠️ Чат уже зарегистрирован.");
        }
    }

    private async Task ShowAllParticipants(long chatId, string chatType)
    {
        if (chatType != "group" && chatType != "supergroup")
        {
            await _botClient.SendTextMessageAsync(chatId, "⚠️ Эта команда доступна только в групповых чатах.");
            return;
        }

        var participants = await _dbContext.Participants
            .Where(p => p.ChatId == chatId)
            .ToListAsync();

        if (!participants.Any())
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Участники не найдены.");
            return;
        }

        var participantList = string.Join("\n", participants.Select(p => $"@{p.UserName} ({p.FullName})"));
        await _botClient.SendTextMessageAsync(chatId, $"👥 Список участников:\n{participantList}");
    }
    
    private async Task HandleStopCommand(long chatId, string chatType, long userId)
    {
        if (!await ValidateGroupAndAdmin(chatId, chatType, userId))
            return;

        var chat = await _dbContext.Chats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.ChatId == chatId);

        if (chat == null || !chat.Participants.Any())
        {
            await _botClient.SendTextMessageAsync(chatId, "⚠️ Нет участников для завершения регистрации.");
            return;
        }

        bool isDistributionSuccessful = await DistributePairs(chatId);

        if (!isDistributionSuccessful)
        {
            return;
        }

        _dbContext.Participants.RemoveRange(chat.Participants);
        chat.IsActive = false;
        await _dbContext.SaveChangesAsync();

        await _botClient.SendTextMessageAsync(chatId, "🛑 Регистрация завершена. Пары распределены, и игра завершена.");
    }

    private async Task HandleResetCommand(long chatId, string chatType, long userId)
    {
        if (!await ValidateGroupAndAdmin(chatId, chatType, userId))
            return;
        
        var chat = await _dbContext.Chats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.ChatId == chatId);

        if (chat != null)
        {
            _dbContext.Participants.RemoveRange(chat.Participants);
            _dbContext.Chats.Remove(chat);
            await _dbContext.SaveChangesAsync();

            await _botClient.SendTextMessageAsync(chatId, "🔄 Игра сброшена. Чат и участники удалены.");
        }
        else
        {
            await _botClient.SendTextMessageAsync(chatId, "⚠️ Чат не зарегистрирован.");
        }
    }

    private async Task HandleJoinCommand(long chatId, long userId, string username, string fullName)
    {
        var chat = await _dbContext.Chats.FirstOrDefaultAsync(c => c.ChatId == chatId);
        if (chat == null || !chat.IsActive)
        {
            await _botClient.SendTextMessageAsync(chatId, "⚠️ Чат не зарегистрирован или игра уже завершена. Сначала используйте команду /start.");
            return;
        }

        var participantExists = await _dbContext.Participants
            .AnyAsync(p => p.TelegramId == userId && p.ChatId == chatId);

        if (!participantExists)
        {
            _dbContext.Participants.Add(new Participant
            {
                TelegramId = userId,
                UserName = username,
                ChatId = chatId,
                FullName = fullName
            });
            await _dbContext.SaveChangesAsync();
            await _botClient.SendTextMessageAsync(chatId, $"✅ @{username}, вы зарегистрированы!");
        }
        else
        {
            await _botClient.SendTextMessageAsync(chatId, $"⚠️ @{username}, вы уже зарегистрированы.");
        }
    }

    private async Task<bool> DistributePairs(long chatId)
    {
        var participants = _dbContext.Participants.Where(p => p.ChatId == chatId).ToList();

        if (participants.Count < 2)
        {
            await _botClient.SendTextMessageAsync(chatId, "❗ Недостаточно участников для распределения пар.");
            return false;
        }

        var shuffled = participants.OrderBy(_ => Guid.NewGuid()).ToList();

        for (int i = 0; i < shuffled.Count; i++)
        {
            var giver = shuffled[i];
            var receiver = shuffled[(i + 1) % shuffled.Count];

            try
            {
                await _botClient.SendTextMessageAsync(giver.TelegramId, $"🎁 Вы должны подарить подарок @{receiver.UserName}!");
            }
            catch (Exception)
            {
                await _botClient.SendTextMessageAsync(
                    chatId,
                    $"⚠️ @{giver.UserName}, бот не может отправить вам личное сообщение. Пожалуйста, отправьте команду /start боту в личных сообщениях."
                );
                return false;
            }
        }

        await _botClient.SendTextMessageAsync(chatId, "✅ Все пары распределены и уведомлены в личных сообщениях!");
        return true;
    }

    private async Task SetBotCommands()
    {
        var groupCommands = new List<BotCommand>
        {
            new BotCommand { Command = "start", Description = "Начать новую игру" },
            new BotCommand { Command = "stop", Description = "Завершить регистрацию и распределить пары" },
            new BotCommand { Command = "reset", Description = "Сбросить текущую игру" },
            new BotCommand { Command = "join", Description = "Присоединиться к игре" },
            new BotCommand { Command = "info", Description = "Информация об участниках" }
        };

        // Устанавливаем команды для групповых и супер-групповых чатов
        var scope = new BotCommandScopeAllGroupChats();

        await _botClient.SetMyCommandsAsync(groupCommands, scope);
    }

    private async Task<bool> ValidateGroupAndAdmin(long chatId, string chatType, long userId, string errorMessage = null)
    {
        if (chatType != "group" && chatType != "supergroup")
        {
            await _botClient.SendTextMessageAsync(chatId, "⚠️ Эта команда доступна только в групповых чатах.");
            return false;
        }

        if (!await IsUserAdmin(chatId, userId))
        {
            await _botClient.SendTextMessageAsync(chatId, errorMessage ?? "❌ У вас нет прав администратора для выполнения этой команды.");
            return false;
        }

        return true;
    }
}