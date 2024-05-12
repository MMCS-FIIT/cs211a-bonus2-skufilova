using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Newtonsoft.Json;

namespace SimpleTGBot
{
    public class TelegramBot
    {
        private readonly string _botToken;
        private readonly string _logFilePath;
        private readonly string _dataFilePath;
        private readonly DramaRecommendationService _dramaRecommendationService;
        private readonly ITelegramBotClient _botClient;

        public TelegramBot(string botToken, string logFilePath, string dataFilePath, DramaRecommendationService dramaRecommendationService)
        {
            _botToken = botToken;
            _logFilePath = logFilePath;
            _dataFilePath = dataFilePath;
            _dramaRecommendationService = dramaRecommendationService;
            _botClient = new TelegramBotClient(_botToken);
        }

        public async Task Run()
        {

            using CancellationTokenSource cts = new CancellationTokenSource();

            ReceiverOptions receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
            };

            _botClient.StartReceiving(
                updateHandler: OnUpdateReceived,
                pollingErrorHandler: OnErrorOccured,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            var me = await _botClient.GetMeAsync(cancellationToken: cts.Token);
            Console.WriteLine($"Бот @{me.Username} запущен.\nДля остановки нажмите клавишу Esc...");

            while (Console.ReadKey().Key != ConsoleKey.Escape) { }

            cts.Cancel();
        }

        private async Task OnUpdateReceived(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message == null)
            {
                return;
            }

            var chatId = update.Message.Chat.Id;
            var messageText = update.Message.Text;

            if (string.IsNullOrWhiteSpace(messageText))
            {
                return;
            }

            LogMessage(chatId, messageText);

            await HandleMessage(botClient, chatId, messageText, cancellationToken);
        }

        private async Task HandleMessage(ITelegramBotClient botClient, long chatId, string messageText, CancellationToken cancellationToken)
        {
            if (messageText.StartsWith(Command.Start))
            {
                await SendStartMessage(botClient, chatId, cancellationToken);
                return;
            }

            if (messageText.StartsWith(Command.Next))
            {
                await SendNextDramaRecommendation(botClient, chatId, cancellationToken);
                return;
            }

            if (messageText.StartsWith(Command.Random))
            {
                await SendRandomDramaRecommendation(botClient, chatId, cancellationToken);
                return;
            }

            if (messageText.StartsWith(Command.About))
            {
                await SendAboutMessage(botClient, chatId, cancellationToken);
                return;
            }


            var dramaRecommendation = _dramaRecommendationService.GetDramaRecommendationByGenre(chatId, messageText);
            if (dramaRecommendation != null)
            {
                await SendDramaRecommendation(botClient, chatId, dramaRecommendation, cancellationToken);
                return;
            }
        }
        private async Task SendSearchResults(ITelegramBotClient botClient, long chatId, IEnumerable<Drama> searchResults, CancellationToken cancellationToken)
        {
            var messageText = "Search results:\n";
            foreach (var drama in searchResults.Take(5)) // limit to 5 results
            {
                messageText += $"{drama.Name} ({drama.Genre})\n";
            }
            await SendMessage(botClient, chatId, messageText, cancellationToken);
        }
        private async Task SendAboutMessage(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var aboutMessage = "Bot name: K-Drama Recommendations \n" +
                              "Version: 1.0\n" +
                              "Creators: [Alexandra Kufilova]";
            await SendMessage(botClient, chatId, aboutMessage, cancellationToken);
        }


        private async Task SendMessage(ITelegramBotClient botClient, long chatId, string messageText, CancellationToken cancellationToken, InlineKeyboardMarkup inlineKeyboardMarkup = null, int replyToMessageId = 0)
        {
            await botClient.SendTextMessageAsync(
                chatId,
                messageText,
                replyMarkup: inlineKeyboardMarkup,
                cancellationToken: cancellationToken
            );
        }

        private async Task SendStartMessage(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var inlineKeyboardMarkup = new InlineKeyboardMarkup(
                new List<InlineKeyboardButton[]>
                {
                    new InlineKeyboardButton[]
                    {
                        InlineKeyboardButton.WithCallbackData("романтика", "button1"),
                        InlineKeyboardButton.WithCallbackData("драма", "button2"),
                        InlineKeyboardButton.WithCallbackData("мелодрама", "button3"),
                    },
                    new InlineKeyboardButton[]
                    {
                        InlineKeyboardButton.WithCallbackData("ужасы", "button1"),
                        InlineKeyboardButton.WithCallbackData("триллер", "button2"),
                        InlineKeyboardButton.WithCallbackData("фантастика", "button3"),
                    },
                    new InlineKeyboardButton[]
                    {
                        InlineKeyboardButton.WithCallbackData("музыка", "button1"),
                        InlineKeyboardButton.WithCallbackData("комедия", "button2"),
                        InlineKeyboardButton.WithCallbackData("боевик", "button3"),
                    },
                }
            );

            await SendMessage(botClient, chatId, "Привет! Я бот, который поможет вам найти интересующую вас дораму. Выберите жанр дорамы и напишите его:", cancellationToken, inlineKeyboardMarkup);
            await SendMessage(botClient, chatId, "Используйте команды:\n/start - начать работу с ботом\n/next - вывод следущей дорамы\n/random - рандомная дорама\n/about - информация о боте\n", cancellationToken);
        }

        private async Task SendDramaRecommendation(ITelegramBotClient botClient, long chatId, Drama dramaRecommendation, CancellationToken cancellationToken)
        {
            using var httpClient = new HttpClient();
            var photoStream = await httpClient.GetStreamAsync(dramaRecommendation.PhotoUrl);
            var photoFile = new InputFile(photoStream, "photo");

            var photo = await botClient.SendPhotoAsync(
                chatId,
                photoFile,
                cancellationToken: cancellationToken
            );

            await SendMessage(botClient, chatId, $"Вам может понравиться: {dramaRecommendation.Name}\nЖанр: {dramaRecommendation.Genre}\nРейтинг: {dramaRecommendation.Rating}\nОписание: {dramaRecommendation.Description}", cancellationToken, replyToMessageId: photo.MessageId);

            await SendMessage(botClient, chatId, "Хотите увидеть следующую дораму? /next", cancellationToken);
        }

        private async Task SendNextDramaRecommendation(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var dramaRecommendation = _dramaRecommendationService.GetRandomDrama();
            await SendDramaRecommendation(botClient, chatId, dramaRecommendation, cancellationToken);
        }

        private async Task SendRandomDramaRecommendation(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var dramaRecommendation = _dramaRecommendationService.GetRandomDrama();
            await SendDramaRecommendation(botClient, chatId, dramaRecommendation, cancellationToken);
        }

        private async Task OnErrorOccured(ITelegramBotClient client, Exception exception, CancellationToken token)
        {
            LogError(exception);
            Console.WriteLine(GetErrorMessage(exception));
            await Task.CompletedTask;
        }

        private void LogMessage(long chatId, string messageText)
        {
            System.IO.File.AppendAllText(_logFilePath, $"{chatId}: {messageText}\n");
        }

        private void LogError(Exception exception)
        {
            System.IO.File.AppendAllText(_logFilePath, $"Error: {exception.Message}\n{exception.StackTrace}\n");
        }

        private string GetErrorMessage(Exception exception)
        {
            return exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",

                _ => exception.ToString()
            };
        }
    }

    public static class Command
    {
        public const string Start = "/start";
        public const string Next = "/next";
        public const string Random = "/random";
        public const string About = "/about";

    }

    public class Drama
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string PhotoUrl { get; set; }
        public double Rating { get; set; }
        public string Genre { get; set; }
    }

    public class DramaRecommendationService
{
    private readonly string _dataFilePath;
    private readonly List<Drama> _dramas;
    private readonly Random _random = new Random();
    private readonly Dictionary<string, List<Drama>> _genreDramas = new Dictionary<string, List<Drama>>();
    private readonly Dictionary<long, List<Drama>> _userRecommendations = new Dictionary<long, List<Drama>>();

    public DramaRecommendationService(string dataFilePath)
    {
        _dataFilePath = dataFilePath;
        _dramas = LoadDramas();
        foreach (var drama in _dramas)
        {
            if (!_genreDramas.ContainsKey(drama.Genre))
            {
                _genreDramas[drama.Genre] = new List<Drama>();
            }
            _genreDramas[drama.Genre].Add(drama);
        }
    }
        public IEnumerable<Drama> SearchDramas(string query)
        {
            return _dramas.Where(d => d.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                                 || d.Genre.Contains(query, StringComparison.OrdinalIgnoreCase)
                                 || d.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        public Drama GetDramaRecommendationByGenre(long chatId, string genre)
    {
        if (!_userRecommendations.ContainsKey(chatId))
        {
            _userRecommendations[chatId] = new List<Drama>();
        }

        var dramasByGenre = _genreDramas[genre];
        var recommendedDramas = _userRecommendations[chatId];

        var availableDramas = dramasByGenre.Except(recommendedDramas).ToList();
        if (availableDramas.Count == 0)
        {
            _userRecommendations[chatId].Clear();
            availableDramas = dramasByGenre;
        }

        var randomIndex = _random.Next(availableDramas.Count);
        var recommendedDrama = availableDramas[randomIndex];
        _userRecommendations[chatId].Add(recommendedDrama);
        return recommendedDrama;
    }

    public Drama GetRandomDrama()
    {
        return _dramas.OrderBy(d => Guid.NewGuid()).FirstOrDefault();
    }

    private List<Drama> LoadDramas()
    {
        var json = System.IO.File.ReadAllText(_dataFilePath);
        return JsonConvert.DeserializeObject<List<Drama>>(json);
    }
}
}