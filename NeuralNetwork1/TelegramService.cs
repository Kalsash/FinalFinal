using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;
using Accord.Neuro;
using System.Data;
using System.Drawing;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace NeuralNetwork1
{
    enum ChatMode
    {
        CHATTING,
        RECOGNIZING,
        CHOOSING
    };
    class TelegramService : IDisposable
    {
        public Telegram.Bot.TelegramBotClient client = null;

        private UpdateTLGMessages formUpdater;
        private readonly AIMLService aimlService = new AIMLService();
        private readonly NeuralNetworkService networkService;
        private BaseNetwork perseptron = null;
        private DatasetProcessor dataset = new DatasetProcessor();
        private string lastRecognizedLetter = "none";
        public bool IsNet = true;
        public bool IsQuiz = false;
        public int QuestionNumber = 0;
        public int RightAnswers = 0;

        Dictionary<long, ChatMode> dialogMode;
        public string Username { get; }

        public string PreviousJoke = "";

        private MagicEye Processor;
        // CancellationToken - инструмент для отмены задач, запущенных в отдельном потоке
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        public TelegramService(BaseNetwork net, UpdateTLGMessages updater)
        {
            var botKey = System.IO.File.ReadAllText("botkey.txt");
            client = new Telegram.Bot.TelegramBotClient(botKey);
            formUpdater = updater;
            perseptron = net;
            networkService = new NeuralNetworkService();
            dialogMode = new Dictionary<long, ChatMode>();
//            client.StartReceiving(HandleUpdateMessageAsync, HandleErrorAsync, new ReceiverOptions
//            {   // Подписываемся только на сообщения
//                AllowedUpdates = new[] { UpdateType.Message }
//            },
//cancellationToken: cts.Token);
            // Пробуем получить логин бота - тестируем соединение и токен
            Username = client.GetMeAsync().Result.Username;
        }

        public void SetNet(BaseNetwork net)
        {
            perseptron = net;
            Processor = new MagicEye(perseptron, dataset);
            formUpdater("Net updated!");
        }
        public void SetNet(BaseNetwork net, DatasetProcessor dataset)
        {
            perseptron = net;
            Processor = new MagicEye(perseptron, dataset);
            formUpdater("Net updated!");
        }

        private async Task HandleUpdateMessageAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            //  Тут очень простое дело - банально отправляем назад сообщения
            var message = update.Message;
            var chatId = message.Chat.Id;
            if (!dialogMode.ContainsKey(chatId))
            {
                dialogMode.Add(chatId, ChatMode.CHATTING);
            }
            var username = message.Chat.FirstName;
            formUpdater("Тип сообщения : " + message.Type.ToString());
            if (message.Type == MessageType.Text)
            {
                var messageText = update.Message.Text.Replace("ё", "е");
                formUpdater($"Received a '{messageText}' message in chat {chatId} with {username}.");
                if (messageText == "/story")
                {
                    List<string> list = new List<string>
                    {
                        "Мужик в зоомагазине спрашивает:\r\n- У вас говорящие попугаи есть?\r\n- Нет, возьмите дятла!\r\n- А он говорить умеет?\r\n- Да, азбукой Морзе!",
                         "Штирлиц пришел домой, вытащил из-под кровати радиостанцию и начал отбивать шифровку в центр. Голос Копеляна за кадром:\r\n\"Штирлиц не знал азбуки Морзе, но он надеялся, что по радостному бибиканью на родине поймут - задание партии выполнено.\"",
                        "- А знаете, что страшнее китайского алфавита?\r\n- Что?\r\n- Китайская азбука Морзе.",
                        "Морзе очень любил с мужиками в баню ходить. Бывало, сидит и наблюдает, как люди моются. Он и азбуку свою создал так: \"Один длинный, два коротких, три коротких, один длинный...\"",
                        "Брат сдавал ЕГЭ в 2013 году. Парень, нужно сказать, способный, но очень ленивый. Есть у него такой же друг. Так вот, решили они выучить азбуку Морзе за пару месяцев, для того, чтобы помогать друг другу на едином гос. экзамене.\r\n\r\nВыучить - выучили, а их по разным аудиториям распределили. Каждый своими силами сдал.",
                        "- С тех пор как я выучил азбуку Морзе, не могу уснуть в дождь. Например, вчера я услышал, что дождь позвал меня выпить. Причем, трижды и по имени...",
                        "Концерт чечёточников свел с ума трех человек из зала, которые знали азбуку Морзе"

                    };
                    Random random = new Random();
                    string randomJoke = list[random.Next(list.Count)];
                    while (randomJoke == PreviousJoke)
                    {
                        randomJoke = list[random.Next(list.Count)];
                    }
                    PreviousJoke = randomJoke;
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: randomJoke,
                        cancellationToken: cancellationToken);
                    return;
                }
                if (messageText == "/help")
                {
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Давайте я познакомлю вас с моим функционалом. \n" +
                        "1) Для распознавания по фото введите команду /morse \n " +
                        "2) Для возвращения в режим разговора введите ключевое слово \"стоп\" \n" +
                        "3)Для перехода в режим рассказа историй введите команду /story \n" +
                        "4) Для того, чтобы узнать, кто я, введите команду /about\n" +
                        "5) Можете проверить свои знания по азбуке Морзе с помощью команды /quiz\n" +
                        "6) Если забудете какую-нибудь из команд введите команду /help, и я буду рад помочь вам снова)",
                        cancellationToken: cancellationToken);
                    return;
                }
                if (messageText == "/morse")
                {
                    lastRecognizedLetter = "none";
                    dialogMode[chatId] = ChatMode.RECOGNIZING;
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Пришлите фото одной из 10 букв Азбуки Морзе: \n1) А . -\n 2) Г - -. \n 3) Е .\n 4) З - -. .\n " +
                        "5) Н - .\n 6) П . - - . \n 7) Т -\n 8) Ц - . - .\n 9) Ш - - - - \n10) Ь - . . -" +
                        "\nРаспознаю с точностью ~90%, так что возможны ошибки!",
                        cancellationToken: cancellationToken);
                    return;
                }
                if (messageText == "/quiz")
                {
                    lastRecognizedLetter = "none";
                    dialogMode[chatId] = ChatMode.CHOOSING;
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Сейчас я задам вам несколько вопросов, чтобы проверить ваши знания по азбуке Морзе. Если вам вдруг надоест отвечать, пишите ключевое слово \"сдаюсь\"",
                        cancellationToken: cancellationToken);
                    return;
                }
                if (messageText == "/about")
                {
                    lastRecognizedLetter = "none";
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Я чат-бот, умею болтать на простые темы, а ещё могу распознать (правда не очень точно) азбуку Морзе. Если хочешь узнать обо мне больше, то просто спрашивай",
                        cancellationToken: cancellationToken);
                    return;
                }
                if (dialogMode[chatId] == ChatMode.CHATTING)
                {
                    await botClient.SendTextMessageAsync(
                    chatId: chatId,
                        text: aimlService.Talk(chatId, username, messageText),
                        cancellationToken: cancellationToken);
                    return;
                }
                if (dialogMode[chatId] == ChatMode.CHOOSING)
                {
                    string ans = messageText;

                    if (messageText.ToLower() == "сдаюсь")
                    {
                        lastRecognizedLetter = "none";
                        await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Викторину пока завершим и продолжим нашу беседу)",
                            cancellationToken: cancellationToken);
                        dialogMode[chatId] = ChatMode.CHATTING;
                        return;
                    }
                    if (ans != "1" && ans != "2" && ans != "3" && ans != "4" && QuestionNumber > 0)
                    {
                        await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Неправильный ввод, попробуйте еще раз!",
                        cancellationToken: cancellationToken);
                        QuestionNumber--;
                    }

                    else
                    {
                        switch (QuestionNumber)
                        {
                            case 1:
                                if (ans == "2")
                                    RightAnswers++;
                                break;
                            case 2:
                                if (ans == "1")
                                    RightAnswers++;
                                break;
                            case 3:
                                if (ans == "4")
                                    RightAnswers++;
                                break;
                            case 4:
                                if (ans == "3")
                                    RightAnswers++;
                                break;
                            case 5:
                                if (ans == "1")
                                    RightAnswers++;
                                break;

                            default:
                                break;
                        }
                    }
                    if (QuestionNumber == 0)
                    {
                        await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Кто создал азбуку Морзе? \n" +
                            "1)Джон Морзе 2) Сэмюэл Морзе 3)Томас Морзе 4) Лейбниц \n" +
                            "Для ответа на вопрос просто введите цифру с правильным ответом: ",
                    cancellationToken: cancellationToken);
                    }
                    if (QuestionNumber == 1)
                    {
                        await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Чем изначально интересовался создатель азбуки Морзе? \n" +
                            "1) Живописью 2) Музыкой 3) Хореографией 4) Литературой \n" +
                            "Для ответа на вопрос просто введите цифру с правильным ответом: ",
                    cancellationToken: cancellationToken);
                    }
                    if (QuestionNumber == 2)
                    {
                        await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "В каком году было отправлено первое сообщение, закодированное с помощью азбуки Морзе? \n" +
                            "1)1841 2) 1842  3) 1843 4) 1844 \n" +
                            "Для ответа на вопрос просто введите цифру с правильным ответом: ",
                    cancellationToken: cancellationToken);
                    }
                    if (QuestionNumber == 3)
                    {
                        await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Сколько времени уйдет на изучение азбуки Морзе по мнению радиолюбителей? \n" +
                            "1)от трех до шести месяцев 2) т двух до пяти месяцев  3)от двух до шести месяцев 4) от трех до пяти месяцев \n" +
                            "Для ответа на вопрос просто введите цифру с правильным ответом: ",
                    cancellationToken: cancellationToken);
                    }
                    if (QuestionNumber == 4)
                    {
                        await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Когда азбука Морзе начала широко использоваться? \n" +
                            "1)после Первой Мировой войны 2) после Второй Мировой войны 3) в 18 веке 4) в 19 веке\n" +
                            "Для ответа на вопрос просто введите цифру с правильным ответом: ",
                    cancellationToken: cancellationToken);
                    }
                    if (QuestionNumber == 5)
                    {
                        if (RightAnswers == 5)
                        {
                            await botClient.SendTextMessageAsync(
                              chatId: chatId,
                              text: RightAnswers.ToString()+ " из 5 правильных ответов. Да вы настоящий знаток Морзе!",
                              cancellationToken: cancellationToken);
                        }
                        if (RightAnswers == 4)
                        {
                            await botClient.SendTextMessageAsync(
                              chatId: chatId,
                              text: RightAnswers.ToString() + " из 5 правильных ответов. Отличный результат!",
                              cancellationToken: cancellationToken);
                        }
                        if (RightAnswers == 3)
                        {
                            await botClient.SendTextMessageAsync(
                              chatId: chatId,
                              text: RightAnswers.ToString() + " из 5 правильных ответов. Хороший результат!",
                              cancellationToken: cancellationToken);
                        }
                        if (RightAnswers < 3)
                        {
                            await botClient.SendTextMessageAsync(
                              chatId: chatId,
                              text: RightAnswers.ToString() + " из 5 правильных ответов. Вам нужно более подробно изучить факты об азбуке Морзе!",
                              cancellationToken: cancellationToken);
                        }
                        dialogMode[chatId] = ChatMode.CHATTING;

                    }
                        QuestionNumber++;


                }
                   

                    if (dialogMode[chatId] == ChatMode.RECOGNIZING)
                {
                    if (messageText.ToLower() == "стоп")
                    {
                        lastRecognizedLetter = "none";
                        await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Распознавание пока остановим и продолжим нашу беседу)",
                            cancellationToken: cancellationToken);
                        dialogMode[chatId] = ChatMode.CHATTING;
                        return;
                    }
                    if (lastRecognizedLetter != "none")
                    {
                        string answer;
                        if (messageText.Length > 1)
                        {
                            answer = "Я бы хотел, конечно, узнать правильную букву, но предположим, что я все-таки угадал";
                        }
                        else if (messageText.ToUpper() == lastRecognizedLetter)
                        {
                            answer = aimlService.Talk(chatId, username, "угадал");
                        }
                        else
                        {
                            answer = aimlService.Talk(chatId, username, "промахнулся");

                        }
                        await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: answer,
                            cancellationToken: cancellationToken);
                        await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Если хотите завершить распознавание, напишите \"стоп\"",
                            cancellationToken: cancellationToken);

                        lastRecognizedLetter = "none";
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Хотите продолжить распознавание по фото или перейдем к личной беседе(для этого напишите ключевое слово \"стоп\")?",
                            cancellationToken: cancellationToken);
                    }
                }
            }
            //  Получение файла (картинки)
            if (message.Type == Telegram.Bot.Types.Enums.MessageType.Photo)
            {
                formUpdater("Picture loadining started");
                if (dialogMode[chatId] != ChatMode.RECOGNIZING)
                {
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Для распознавания по фото, нужно применить команду /morse",
                        cancellationToken: cancellationToken);
                    return;
                }
                var photoId = message.Photo.Last().FileId;
                Telegram.Bot.Types.File fl = await client.GetFileAsync(photoId, cancellationToken: cancellationToken);
                var imageStream = new MemoryStream();
                await client.DownloadFileAsync(fl.FilePath, imageStream, cancellationToken: cancellationToken);
                var img = System.Drawing.Image.FromStream(imageStream);

                System.Drawing.Bitmap bm = new System.Drawing.Bitmap(img);
                var p = "";
                //if (Processor.ProcessImage(bm) && IsNet)
                //     p = DatasetProcessor.LetterTypeToString(Processor.currentType);
                //else
                //    p = DatasetProcessor.LetterTypeToString(networkService.predict(bm));
                p = DatasetProcessor.LetterTypeToString(networkService.predict(bm));
                await botClient.SendTextMessageAsync(
                     chatId: chatId,
                     text: aimlService.Talk(chatId, username, $"предсказываю {p}"),
                     cancellationToken: cancellationToken);
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: aimlService.Talk(chatId, username, $"жду правду"),
                    cancellationToken: cancellationToken);
                lastRecognizedLetter = p;
                //formUpdater(DatasetProcessor.LetterTypeToString(p));
                formUpdater("Picture recognized!");
                return;
            }

            if (message.Type == MessageType.Video)
            {
                await client.SendTextMessageAsync(message.Chat.Id, aimlService.Talk(chatId, username, "Видео"), cancellationToken: cancellationToken);
                return;
            }
            if (message.Type == MessageType.Audio)
            {
                await client.SendTextMessageAsync(message.Chat.Id, aimlService.Talk(chatId, username, "Аудио"), cancellationToken: cancellationToken);
                return;
            }
            formUpdater(message.Text);
            return;
        }
        Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var apiRequestException = exception as ApiRequestException;
            if (apiRequestException != null)
                Console.WriteLine($"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}");
            else
                Console.WriteLine(exception.ToString());
            return Task.CompletedTask;
        }

        public bool Act()
        {
            try
            {
                client.StartReceiving(HandleUpdateMessageAsync, HandleErrorAsync, new ReceiverOptions
                {   // Подписываемся только на сообщения
                    AllowedUpdates = new[] { UpdateType.Message }
                },
                cancellationToken: cts.Token);
                // Пробуем получить логин бота - тестируем соединение и токен
                formUpdater($"Connected as {client.GetMeAsync().Result}");
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }
        public void Dispose()
        {
            // Заканчиваем работу - корректно отменяем задачи в других потоках
            // Отменяем токен - завершатся все асинхронные таски
            cts.Cancel();
        }
    }
}
