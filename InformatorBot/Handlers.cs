using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace InformatorBot
{
    public class Handlers
    {
        private static string botName = "Информатор";
        private static string firstMessage = $"Вас приветствует чат-бот  {botName}. " +
                                             $"С моей помощью ИТ специалисты будут оперативно сообщать о важных событиях в используемых Вами системах.";

        private static Models.User? user;

        private static readonly Services _services = new Services(user!);
        private static readonly ApplicationContext? _context = new ApplicationContext();

        /// <summary>
        /// Обработчик исключений
        /// </summary>
        /// <param name="botClient">Данные бота</param>
        /// <param name="exception">Исключение</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Сообщение с кодом и расшифровкой исключения</returns>
        public static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Обработчик событий
        /// </summary>
        /// <param name="botClient">Данные бота</param>
        /// <param name="update">Событие</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Переход в нужный метод на основе полученных данных в "update"</returns>
        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.MyChatMember || update.Type == UpdateType.Message || update.Type == UpdateType.CallbackQuery)
            {
                //Обработка события добавления бота в чат
                if (update.MyChatMember != null)
                {
                    if (update.MyChatMember.NewChatMember.Status == ChatMemberStatus.Left)
                    {
                        return;
                    }

                    await botClient.SendTextMessageAsync(chatId: update.MyChatMember!.Chat.Id, text: $"{firstMessage} ID группы: {update.MyChatMember.Chat.Id}. Сообщите его администратору бота.");
                    return;
                }

                if (update.Message != null)
                {
                    //обработка события об исключении бота из чата
                    if (update.Message.Type == MessageType.ChatMemberLeft)
                    {
                        if (update.Message.LeftChatMember!.IsBot)
                        {
                            var chats = _context!.ChatGroups!.Where(s => s.ChatId == update.Message.Chat!.Id.ToString()).ToList();
                            if (chats != null)
                            {
                                foreach (var chat in chats)
                                {
                                    chat.IsDeleted = true;
                                    _context.SaveChanges();
                                }
                                return;
                            }
                        }

                        return;
                    }
                    //обработка события о добавлении бота в чат
                    if (update.Message.Type == MessageType.ChatMembersAdded)
                    {
                        await _services.AddChat(botClient, update.Message!);
                        return;
                    }
                    //обработка попытки отправить боту сообщение из чата
                    if (update.Message.Chat.Type == ChatType.Group || update.Message.Chat.Type == ChatType.Supergroup)
                    {
                        await botClient.SendTextMessageAsync(chatId: update.Message!.Chat.Id, text: "Боту нельзя отправлять сообщения из группы!");
                        return;
                    }
                    else
                        user = _context?.Users?.Include(r => r.Role).FirstOrDefault(w => w.Id == update.Message!.Chat.Id.ToString());
                }
                else
                    //обработка, если пришло не сообщение, а callback от клавиатуры
                    user = _context?.Users?.Include(r => r.Role).FirstOrDefault(w => w.Id == update.CallbackQuery!.From.Id.ToString());
            }
            else
            {
                await UnknownUpdateHandlerAsync(botClient, update);
                return;
            }

            if (user == null)
            {
                await botClient.SendTextMessageAsync(chatId: update.Message!.Chat.Id, text: "У Вас нет прав на отправку сообщений боту!");
                return;
            }

            var handler = update.Type switch
            {
                UpdateType.Message => BotOnMessageReceived(botClient, update.Message!),
                UpdateType.CallbackQuery => BotOnCallbackQueryReceived(botClient, update.CallbackQuery!),
                _ => UnknownUpdateHandlerAsync(botClient, update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(botClient, exception, cancellationToken);
            }
        }

        /// <summary>
        /// Обработка входящего сообщения
        /// </summary>
        /// <param name="botClient">Данные от бота</param>
        /// <param name="message">Сообщение</param>
        /// <returns>Встроенная клавиатура с кнопками-группами для рассылки</returns>
        private static async Task BotOnMessageReceived(ITelegramBotClient botClient, Message message)
        {
            Console.WriteLine($"Получено сообщение: {message.Text}");
            if (message.Type != MessageType.Text)
                return;

            //В условиях ниже бот проверяет - если предыдущее сообщение содержит тот, или иной шаблон, то вызывается тот или иной метод для дальнейшей обработки запроса
            if (message.ReplyToMessage != null && message.ReplyToMessage.Text!.Contains("Отправьте мне название новой группы."))
            {
                await _services.AddGroup(botClient, message);
                return;
            }

            if (message.ReplyToMessage != null && message.ReplyToMessage.Text!.Contains("Отправьте мне ID чата, который нужно добавить в группу"))
            {
                _services.IdValidation(_services.AddChatToGroup, message, botClient);
                return;
            }

            if (message.ReplyToMessage != null && message.ReplyToMessage.Text!.Contains("Отправьте мне сообщение, которое нужно разослать в группу"))
            {
                await _services.SendNewsletter(botClient, message);
                return;
            }

            if (message.ReplyToMessage != null && message.ReplyToMessage.Text!.Contains("Отправьте мне ID чата, который нужно удалить из группы"))
            {
                _services.IdValidation(_services.DeleteChatFromGroup, message, botClient);
                return;
            }

            if (message.ReplyToMessage != null && user!.RoleId == (int)RoleType.SuperAdmin && (message.ReplyToMessage.Text!.Contains("Отправьте мне id нового пользователя") ||
                                                  message.ReplyToMessage!.Text!.Contains("Теперь отправьте мне ФИО нового пользователя.") ||
                                                  message.ReplyToMessage.Text!.Contains("Осталось выбрать роль нового пользователя.")))
            {
                if (message.ReplyToMessage.Text!.Contains("Отправьте мне id нового пользователя"))
                {
                    _services.IdValidation(_services.AddUser, message, botClient);
                    return;
                }

                await _services.AddUser(botClient, message);
                return;
            }

            if (message.ReplyToMessage != null && user!.RoleId == (int)RoleType.SuperAdmin && message.ReplyToMessage.Text!.Contains("Отправьте мне id пользователя, которого нужно удалить"))
            {
                _services.IdValidation(_services.DeleteUser, message, botClient);
                return;
            }

            // Обработка команд бота
            var action = message.Text switch
            {
                "/addgroup" => botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Отправьте мне название новой группы.",
                    replyMarkup: new ForceReplyMarkup { Selective = true }),

                "/addchattogroup" => SendInlineKeyboard(botClient, message),

                "/sendnewsletter" => SendInlineKeyboard(botClient, message),

                "/removechatfromgroup" => SendInlineKeyboard(botClient, message),

                "/getlistofusers" => _services.GetListOfUsers(botClient, message),

                "/adduser" => _services.AddUser(botClient, message),

                "/deleteuser" => _services.DeleteUser(botClient, message),

                _ => Usage(botClient, message)
            };
            Message sentMessage = await action;
            Console.WriteLine($"Сообщение было отправлено с ID: {sentMessage.MessageId}");

            //Клавиатура с группами для рассылки
            static async Task<Message> SendInlineKeyboard(ITelegramBotClient botClient, Message message)
            {
                // Симуляция отправки сообщения
                await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                await Task.Delay(500);

                var groups = _context!.Groups!.Include(i => i.Users).Where(w => w.Users!.Any(u => u.Id == user!.Id)).ToList();

                //Получаем клавиатуру в заисимости от количества групп в базе данных
                InlineKeyboardMarkup inlinekeyboard = _services.GroupsInlineKeyboard(groups!);

                if (message.Text == "/addchattogroup")
                {
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: "Выберите группу, в которую нужно добавить чат:",
                                                            replyMarkup: inlinekeyboard);
                }

                if (message.Text == "/removechatfromgroup")
                {
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: "Выберите группу, из которой нужно удалить чат:",
                                                            replyMarkup: inlinekeyboard);
                }

                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: "Выберите группу для отправки Вашего сообщения:",
                                                            replyMarkup: inlinekeyboard);
            }

            static async Task<Message> Usage(ITelegramBotClient botClient, Message message)
            {
                // Симуляция отправки сообщения
                await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                await Task.Delay(500);

                const string usage = "Привет! Вот что я умею:\n" +
                                     "/sendnewsletter - отправка рассылки\n" +
                                     "/addgroup - добавить группу для рассылки\n" +
                                     "/addchattogroup - добавить чат в группу для рассылки\n" +
                                     "/removechatfromgroup - удалить чат из группы для рассылки\n" +
                                     "/adduser - добавить пользователя\n" +
                                     "/deleteuser - удалить пользователя\n" +
                                     "/getlistofusers - получить список пользователей\n";

                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: usage,
                                                            replyMarkup: new ReplyKeyboardRemove());
            }
        }

        /// <summary>
        /// Обработка callback-а, получаемого от клавиатуры
        /// </summary>
        /// <param name="botClient">Данные бота</param>
        /// <param name="callbackQuery">Данные callback-a</param>
        /// <returns>Отправка сообщения в определенную группу на основе данных от callback-а</returns>
        private static async Task BotOnCallbackQueryReceived(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            if (callbackQuery.Message?.Text == "Выберите группу, в которую нужно добавить чат:")
            {
                await botClient.SendTextMessageAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: $"Отправьте мне ID чата, который нужно добавить в группу {callbackQuery.Data}",
                    replyMarkup: new ForceReplyMarkup { Selective = true }
                    );
            }
            else if (callbackQuery.Message?.Text == "Выберите группу, из которой нужно удалить чат:")
            {
                await botClient.SendTextMessageAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: $"Отправьте мне ID чата, который нужно удалить из группы {callbackQuery.Data}",
                    replyMarkup: new ForceReplyMarkup { Selective = true }
                    );
            }
            else if (callbackQuery.Message?.Text == "Осталось выбрать роль нового пользователя.")
            {
                if (callbackQuery.Data == "Суперадмин")
                {
                    _services.SaveUser((int)RoleType.SuperAdmin);
                }
                if (callbackQuery.Data == "Администратор")
                {
                    _services.SaveUser((int)RoleType.Admin);
                }
                if (callbackQuery.Data == "Оператор")
                {
                    _services.SaveUser((int)RoleType.Operator);
                }

                await botClient.SendTextMessageAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: $"Пользователь добавлен!"
                    );
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId: callbackQuery.Message!.Chat.Id,
                    text: $"Отправьте мне сообщение, которое нужно разослать в группу {callbackQuery.Data}",
                    replyMarkup: new ForceReplyMarkup { Selective = true }
                    );
            }
        }

        /// <summary>
        /// Обработка типов запросов, которые не используются, например, изменение текста сообщения
        /// </summary>
        /// <param name="botClient">Данные бота</param>
        /// <param name="update">Данные запроса</param>
        /// <returns>Запись в консоли</returns>
        private static Task UnknownUpdateHandlerAsync(ITelegramBotClient botClient, Update update)
        {
            Console.WriteLine($"Unknown update type: {update.Type}");
            return Task.CompletedTask;
        }
    }
}
