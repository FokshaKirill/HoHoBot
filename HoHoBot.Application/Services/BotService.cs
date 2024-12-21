using HoHoBot.Domain.Entities;
using HoHoBot.Domain.ValueObjects.Enums;
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

    private async Task HandleStartCommand(long chatId, string chatType, long userId)
    {
        if (!await ValidateGroupAndAdmin(chatId, chatType, userId))
            return;

        var chat = await _dbContext.Chats.FirstOrDefaultAsync(c => c.ChatId == chatId);

        if (chat == null)
        {
            _dbContext.Chats.Add(new Chat
            {
                ChatId = chatId,
                Name = chatId.ToString(),
                ChatType = chatType,
                GameState = GameState.Registration 
            });
            await _dbContext.SaveChangesAsync();
            await _botClient.SendTextMessageAsync(chatId, "✅ Чат зарегистрирован и регистрация участников начата!");
        }
        else
        {
            switch (chat.GameState)
            {
                case GameState.Completed:
                    chat.GameState = GameState.Registration;
                    _dbContext.Update(chat);
                    await _dbContext.SaveChangesAsync();
                    await _botClient.SendTextMessageAsync(chatId, "✅ Игра завершена. Начинаем новую регистрацию участников!");
                    break;

                case GameState.Registration:
                    await _botClient.SendTextMessageAsync(chatId, "⚠️ Регистрация участников уже идет.");
                    break;

                case GameState.InProgress:
                    await _botClient.SendTextMessageAsync(chatId, "⚠️ Игра уже началась.");
                    break;

                default:
                    await _botClient.SendTextMessageAsync(chatId, "⚠️ Чат уже зарегистрирован.");
                    break;
            }
        }
    }

    private async Task HandleJoinCommand(long chatId, long userId, string username, string fullName)
    {
        var chat = await _dbContext.Chats.FirstOrDefaultAsync(c => c.ChatId == chatId);
        if (chat == null)
        {
            await _botClient.SendTextMessageAsync(chatId, "⚠️ Чат не зарегистрирован. Сначала используйте команду /start.");
            return;
        }

        if (chat.GameState != GameState.Registration)
        {
            string message = chat.GameState switch
            {
                GameState.Completed => "⚠️ Регистрация завершена. Сначала используйте команду /start для новой игры.",
                GameState.InProgress => "⚠️ Игра уже началась. Регистрация участников невозможна.",
                _ => "⚠️ Чат неактивен. Используйте команду /start для начала регистрации."
            };
            await _botClient.SendTextMessageAsync(chatId, message);
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

            if (!await HasUserReceivedMessageAsync(userId))
            {
                await _botClient.SendTextMessageAsync(chatId, 
                    $"⚠️ @{username}, пожалуйста, напишите боту в личных сообщениях, чтобы он смог отправлять вам уведомления!");
            }
        }
        else
        {
            await _botClient.SendTextMessageAsync(chatId, $"⚠️ @{username}, вы уже зарегистрированы.");
        }
    }

    private async Task HandleStopCommand(long chatId, string chatType, long userId)
    {
        if (!await ValidateGroupAndAdmin(chatId, chatType, userId))
            return;

        var chat = await _dbContext.Chats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.ChatId == chatId);

        if (chat == null || !chat.Participants.Any())
        {
            await _botClient.SendTextMessageAsync(chatId, "⚠️ Нет участников для завершения регистрации.");
            return;
        }

        if (chat.GameState != GameState.Registration)
        {
            string message = chat.GameState switch
            {
                GameState.InProgress => "⚠️ Игра уже началась. Вы не можете остановить регистрацию.",
                GameState.Completed => "⚠️ Игра уже завершена. Для начала новой игры используйте /start.",
                _ => "⚠️ Чат неактивен или команда недоступна в текущем состоянии."
            };
            await _botClient.SendTextMessageAsync(chatId, message);
            return;
        }

        bool isDistributionSuccessful = await DistributePairs(chatId);

        if (!isDistributionSuccessful)
        {
            return;
        }

        chat.GameState = GameState.Completed;
        _dbContext.Participants.RemoveRange(chat.Participants);
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
    
    private async Task<bool> HasUserReceivedMessageAsync(long userId)
    {
        return await _dbContext.SentMessages.AnyAsync(sm => sm.TelegramId == userId);
    }

    private async Task<bool> DistributePairs(long chatId)
    {
        var participants = _dbContext.Participants.Where(p => p.ChatId == chatId).ToList();

        if (participants.Count < 2)
        {
            await _botClient.SendTextMessageAsync(chatId, "❗ Недостаточно участников для распределения пар.");
            return false;
        }

        var problematicUsers = new List<string>();
        var validParticipants = new List<Participant>();

        foreach (var participant in participants)
        {
            if (await CanCommunicateWithUser(participant.TelegramId))
            {
                validParticipants.Add(participant);
            }
            else
            {
                problematicUsers.Add($"@{participant.UserName}");
            }
        }

        if (problematicUsers.Any())
        {
            await _botClient.SendTextMessageAsync(
                chatId,
                $"⚠️ Не удалось связаться со следующими участниками: {string.Join(", ", problematicUsers)}. Попросите их написать команду /start боту в личных сообщениях."
            );
        }

        if (validParticipants.Count < 2)
        {
            await _botClient.SendTextMessageAsync(chatId, "❗ Недостаточно доступных участников для распределения пар.");
            return false;
        }

        var shuffled = validParticipants.OrderBy(_ => Guid.NewGuid()).ToList();

        for (int i = 0; i < shuffled.Count; i++)
        {
            var giver = shuffled[i];
            var receiver = shuffled[(i + 1) % shuffled.Count];

            try
            {
                await SendMessageAndLogAsync(giver.TelegramId, $"🎁 Вы должны подарить подарок @{receiver.UserName}!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при отправке сообщения @{giver.UserName}: {ex.Message}");
                await _botClient.SendTextMessageAsync(
                    chatId,
                    $"⚠️ Ошибка при уведомлении @{giver.UserName}. Возможно, бот заблокирован."
                );
            }
        }

        await _botClient.SendTextMessageAsync(chatId, "✅ Все пары распределены и уведомлены в личных сообщениях!");
        return true;
    }

    private async Task<bool> CanCommunicateWithUser(long userId)
    {
        bool messageExists = _dbContext.SentMessages.Any(x => x.TelegramId == userId);
        
        if (!messageExists) 
        {
            try
            {
                await _botClient.SendTextMessageAsync(userId, "Проверка доступности бота.");
                return true;
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException ex)
            {
                if (ex.Message.Contains("Forbidden") || ex.Message.Contains("bot was blocked by the user"))
                {
                    Console.WriteLine($"Бот заблокирован пользователем: {userId}");
                    return false;
                }

                throw;
            }
            catch
            {
                return false; 
            }
        }
        else
        {
            return true;
        }
    }

    private async Task SendMessageAndLogAsync(long userId, string message)
    {
        try
        {
            await _botClient.SendTextMessageAsync(userId, message);

            bool messageExists = _dbContext.SentMessages.Any(x => x.TelegramId == userId);

            if (!messageExists)
            {
                _dbContext.SentMessages.Add(new SentMessage
                {
                    TelegramId = userId,
                    SentAt = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Telegram.Bot.Exceptions.ApiRequestException ex)
        {
            if (ex.Message.Contains("Forbidden") || ex.Message.Contains("bot was blocked by the user"))
            {
                Console.WriteLine($"Пользователь заблокировал бота: {userId}");
            }
            else
            {
                Console.WriteLine($"Ошибка отправки сообщения пользователю {userId}: {ex.Message}");
            }
        }
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
}