using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace InformatorBot
{
    public class Services
    {
        private readonly Models.User? _user;
        private readonly Models.User? newUser = new Models.User();

        private const long logChatId = -0; //Чат-ИД для отправки логов в специальный чат

        private static readonly ApplicationContext? _context = new ApplicationContext();

        public Services(Models.User user)
        {
            _user = user;
        }

        /// <summary>
        /// Добавление чата
        /// </summary>
        /// <param name="botClient">Данные бота</param>
        /// <param name="message">Сообщение с данными по чату, куда добавляется бот. Отсюда берем ID и название чата</param>
        /// <returns>Добавление чата в базу данных</returns>
        public Task<Message> AddChat(ITelegramBotClient botClient, Message message)
        {
            var chat = _context?.Chats?.FirstOrDefault(w => w.ChatId == message.Chat.Id.ToString());
            if (chat == null)
            {
                chat = new Chat { ChatId = message.Chat.Id.ToString(), Name = message.Chat.Title };
                _context?.Chats?.Add(chat);
                _context?.SaveChanges();
            }

            return botClient.SendTextMessageAsync(chatId: logChatId, text: $"{message.From!.FirstName} {message.From.LastName} подключил к боту чат '{message.Chat.Title}'!");
        }

        /// <summary>
        /// Добавление группы
        /// </summary>
        /// <param name="botClient">Данные бота</param>
        /// <param name="message">Сообщение с названием новой группы</param>
        /// <returns>Добавление группы для рассылки и отправка уведомления об успешности операции</returns>
        public Task<Message> AddGroup(ITelegramBotClient botClient, Message message)
        {
            if (_user!.RoleId == (int)RoleType.SuperAdmin || _user.RoleId == (int)RoleType.Admin)
            {
                var group = _context?.Groups?.FirstOrDefault(w => w.Title == message.Text);
                if (group == null)
                {
                    group = new Group { Title = message.Text };
                    _context?.Groups?.Add(group);
                    _context?.SaveChanges();
                    botClient.SendTextMessageAsync(chatId: logChatId, text: $"Добавлена группа '{message.Text}'!");
                    return botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"Группа '{message.Text}' добавлена!");
                }

                return botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"Группа '{message.Text}' уже была ранее добавлена!");
            }
            else
                return botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"У Вас нет прав на добавление групп!");
        }

        /// <summary>
        /// Добавление чата в группу
        /// </summary>
        /// <param name="botClient">Данные бота</param>
        /// <param name="message">В сообщении получаем ID чата, который нужно добавить в группу для рассылки</param>
        /// <returns>Добавление чата в группу и отправка уведомления об успешности операции</returns>
        public Task<Message> AddChatToGroup(ITelegramBotClient botClient, Message message)
        {
            if (_user!.RoleId == (int)RoleType.SuperAdmin || _user.RoleId == (int)RoleType.Admin)
            {
                var groupTitle = message.ReplyToMessage?.Text?.Replace("Отправьте мне ID чата, который нужно добавить в группу ", "");
                var group = _context?.Groups?.FirstOrDefault(w => w.Title == groupTitle);
                var chat = _context?.Chats?.FirstOrDefault(w => w.ChatId == message.Text);
                if (chat == null)
                {
                    chat = new Chat { ChatId = message.Text };
                    _context?.Chats?.Add(chat);
                    _context?.SaveChanges();
                }

                var groups = _context?.ChatGroups?.Include(w => w.Group).Where(w => w.Group!.Title == groupTitle && w.ChatId == message.Text).ToList();
                if (groups?.Count == 0)
                {
                    var newChatGroup = new ChatGroup { GroupId = group!.GroupId, ChatId = message.Text };
                    _context?.ChatGroups?.Add(newChatGroup);
                    _context?.SaveChanges();
                    botClient.SendTextMessageAsync(chatId: logChatId, text: $"{message.From!.FirstName} {message.From.LastName} " +
                                                                             $"добавил в группу '{groupTitle}' чат '{chat.Name}'!");
                    return botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"Чат '{chat.Name}' добавлен в группу '{groupTitle}'!");
                }
                else
                {
                    foreach (var item in groups!)
                    {
                        if (item.IsDeleted == true)
                        {
                            item.IsDeleted = false;
                            _context?.SaveChanges();
                            botClient.SendTextMessageAsync(chatId: logChatId, text: $"{message.From!.FirstName} {message.From.LastName} " +
                                                                                     $"добавил в группу '{groupTitle}' чат '{chat.Name}'!");
                            return botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"Чат '{chat.Name}' добавлен в группу '{groupTitle}'!");
                        }
                    }
                }

                return botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"Чат '{chat.Name}' уже был ранее добавлен в группу '{groupTitle}'!");
            }
            else
            {
                return botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"У Вас нет прав на добавление чатов в группы!");
            }

        }

        /// <summary>
        /// Исключение чата из группы
        /// </summary>
        /// <param name="botClient">Данные бота</param>
        /// <param name="message">В сообщении получаем ID чата, который нужно исключить из группы для рассылки</param>
        /// <returns>Исключение чата из группы и отправка уведомления об успешности операции</returns>
        public Task<Message> DeleteChatFromGroup(ITelegramBotClient botClient, Message message)
        {
            if (_user!.RoleId == (int)RoleType.SuperAdmin || _user.RoleId == (int)RoleType.Admin)
            {
                var groupTitle = message.ReplyToMessage?.Text?.Replace("Отправьте мне ID чата, который нужно удалить из группы ", "");
                var group = _context?.Groups?.FirstOrDefault(w => w.Title == groupTitle);
                var groups = _context?.ChatGroups?.Include(w => w.Group).Where(w => w.Group!.Title == groupTitle && w.ChatId == message.Text && w.IsDeleted == false).ToList();
                if (groups?.Count != 0)
                {
                    foreach (var item in groups!)
                    {
                        item.IsDeleted = true;
                        _context?.SaveChanges();
                    }

                    botClient.SendTextMessageAsync(chatId: logChatId, text: $"{message.From!.FirstName} {message.From!.LastName} " +
                                                                             $"удалил из группы '{groupTitle}' чат '{message.Text}'!");
                    return botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"Чат '{message.Text}' удален из группы '{groupTitle}'!");
                }

                return botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"Чат '{message.Text}' уже был удален из группы '{groupTitle}' или еще не добавлен в неё!");
            }
            else
            {
                return botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"У Вас нет прав на удаление чатов из групп!");
            }
        }

        /// <summary>
        /// Отправка рассылки
        /// </summary>
        /// <param name="botClient">Данные от бота</param>
        /// <param name="message">Сообщение для отправки</param>
        /// <returns>Отправка сообщения в чаты выбранной группы для рассылки</returns>
        public async Task<Task> SendNewsletter(ITelegramBotClient botClient, Message message)
        {
            var groupCount = 0;

            var groupTitle = message.ReplyToMessage?.Text?.Replace("Отправьте мне сообщение, которое нужно разослать в группу ", "");

            var chatGroups = _context?.ChatGroups?.Include(g => g.Group).Where(w => w.Group.Title == groupTitle && w.IsDeleted == false);

            foreach (var chatid in chatGroups!)
            {
                groupCount++;
                if (groupCount >= 20)
                {
                    groupCount = 0;
                    Thread.Sleep(1000);
                }

                await botClient.SendTextMessageAsync(
                            chatId: chatid.ChatId!,
                            text: message.Text!);
            }

            await botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"Сообщение отправлено в группу {groupTitle}");
            Console.WriteLine($"Сообщение отправлено в группу '{groupTitle}'");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Добавление пользователя
        /// </summary>
        /// <param name="botClient">Данные бота</param>
        /// <param name="message">Сообщение, на базе которого происходит добавление id или ФИО пользователя</param>
        /// <returns>В данном метооде добавляются только id и ФИО пользователя. Добавление роли и сохранение нового пользователя в базе данных происходит в методе BotOnCallbackQueryReceived</returns>
        public Task<Message> AddUser(ITelegramBotClient botClient, Message message)
        {
            if (_user!.RoleId == (int)RoleType.SuperAdmin)
            {
                if (message.ReplyToMessage != null && message.ReplyToMessage.Text!.Contains("Отправьте мне id нового пользователя"))
                {
                    newUser!.Id = message.Text;
                    return botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Теперь отправьте мне ФИО нового пользователя.",
                    replyMarkup: new ForceReplyMarkup { Selective = true });
                }
                if (message.ReplyToMessage != null && message.ReplyToMessage.Text!.Contains("Теперь отправьте мне ФИО нового пользователя."))
                {
                    InlineKeyboardMarkup rolesInlineKeyoard = RolesInlineKeyboard();
                    newUser!.Name = message.Text;
                    return botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Осталось выбрать роль нового пользователя.",
                    replyMarkup: rolesInlineKeyoard);
                }

                return botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Отправьте мне id нового пользователя",
                    replyMarkup: new ForceReplyMarkup { Selective = true });
            }

            return botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: "У Вас нет прав на добавление пользователей!");
        }

        /// <summary>
        /// Удаление пользователя
        /// </summary>
        /// <param name="botClient">Данные бота</param>
        /// <param name="message">Команда "/deleteuser" и сообщения от бота, ожидающие ответа</param>
        /// <returns>Удаление пользователя и уведомление об успешности операции</returns>
        public Task<Message> DeleteUser(ITelegramBotClient botClient, Message message)
        {
            if (_user!.RoleId == (int)RoleType.SuperAdmin && message.Text == "/deleteuser")
            {
                return botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Отправьте мне id пользователя, которого нужно удалить",
                replyMarkup: new ForceReplyMarkup { Selective = true });
            }
            else if (_user!.RoleId == (int)RoleType.SuperAdmin && message.ReplyToMessage!.Text == "Отправьте мне id пользователя, которого нужно удалить")
            {
                var deletedUser = _context!.Users!.FirstOrDefault(w => w.Id == message.Text);
                _context!.Users!.Remove(deletedUser!);
                _context.SaveChanges();
                botClient.SendTextMessageAsync(chatId: logChatId, text: $"{message.From!.FirstName} {message.From.LastName} удалил пользователя {deletedUser!.Name}");
                return botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: "Пользователь удален!");
            }

            return botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: "У Вас нет прав на удаление пользователей!");
        }

        /// <summary>
        /// Получение списка пользователей
        /// </summary>
        /// <param name="botClient">Данные бота</param>
        /// <param name="message">Команда "/getlistofusers"</param>
        /// <returns></returns>
        public Task<Message> GetListOfUsers(ITelegramBotClient botClient, Message message)
        {
            string list = "Cписок пользователей:\n";
            var listOfUsers = _context!.Users!.ToList();
            foreach (var item in listOfUsers)
            {
                list = $"{list}{item.Name} (ID {item.Id})\n";
            }
            return botClient.SendTextMessageAsync(chatId: message.Chat.Id!, text: list);
        }

        /// <summary>
        /// Клавиатура с группами для рассылки
        /// </summary>
        /// <param name="groups">Список групп для рассылки</param>
        /// <returns></returns>
        public InlineKeyboardMarkup GroupsInlineKeyboard(List<Group> groups)
        {
            // Количество кнопок в одном ряду
            const int buttonsCount = 3;
            int countRow = groups.Count / buttonsCount + 1;

            InlineKeyboardButton[][] inlineKeyboardButtons = new InlineKeyboardButton[countRow][];

            var inlinekeyboard = new InlineKeyboardMarkup(inlineKeyboardButtons);

            for (int i = 0; i < countRow - 1; i++)
            {
                inlineKeyboardButtons[i] = new InlineKeyboardButton[buttonsCount];
            }
            inlineKeyboardButtons[countRow - 1] = new InlineKeyboardButton[groups.Count % buttonsCount];

            int count = 0;
            int row = 0;
            foreach (var group in groups!)
            {
                inlineKeyboardButtons[row][count] = InlineKeyboardButton.WithCallbackData(group.Title!);
                count++;
                if (count == buttonsCount)
                {
                    row++;
                    count = 0;
                }
            }

            return inlinekeyboard;
        }

        /// <summary>
        /// Клавиатура с ролями пользователей
        /// </summary>
        /// <returns>Клавиатура с ролями пользователей</returns>
        private static InlineKeyboardMarkup RolesInlineKeyboard()
        {
            InlineKeyboardMarkup rolesInlineKeyboard = new(
                new[]
                {
                    // first row
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Суперадмин"),
                        InlineKeyboardButton.WithCallbackData("Администратор"),
                        InlineKeyboardButton.WithCallbackData("Оператор")
                    },
                });

            return rolesInlineKeyboard;
        }

        /// <summary>
        /// Сохраниние нового пользователя после выбора роли
        /// </summary>
        /// <param name="roleId">Id роли</param>
        public void SaveUser(int roleId)
        {
            newUser!.RoleId = roleId;
            _context!.Users!.Add(newUser);
            _context.SaveChanges();
        }

        /// <summary>
        /// Валидация ID чата
        /// </summary>
        /// <param name="func">Метод, в которых обрабатывается ID чата</param>
        /// <param name="message">Сообщение с ID чата</param>
        /// <param name="telegramBotClient">Данные бота</param>
        public async void IdValidation(Func<ITelegramBotClient, Message, Task<Message>> func, Message message, ITelegramBotClient telegramBotClient)
        {
            if (Regex.IsMatch(message.Text!, "^-?[0-9]+$"))
            {
                await func(telegramBotClient, message);
                return;
            }
            else
            {
                await telegramBotClient.SendTextMessageAsync(chatId: message.Chat.Id, text: "ID не может содержать буквы и другие символы, кроме -, " +
                                                                                            "проверьте пожалуйста и повторите операцию!");
                return;
            }
        }
    }
}
