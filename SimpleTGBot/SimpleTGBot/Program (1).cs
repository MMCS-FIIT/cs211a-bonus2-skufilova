using System;
namespace SimpleTGBot;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var botToken = "6540476547:AAH0FWgCKSxT1BiwLVM4FdKm28gXoCz44jI";
        var logFilePath = "C:\\Users\\Srrgei\\Desktop\\SimpleTGBot\\SimpleTGBot\\log.txt";
        var dataFilePath = "C:\\Users\\Srrgei\\Desktop\\SimpleTGBot\\SimpleTGBot\\dramas.json";
        var dramaRecommendationService = new DramaRecommendationService(dataFilePath);
        var telegramBot = new TelegramBot(botToken, logFilePath, dataFilePath, dramaRecommendationService);
        await telegramBot.Run();
    }

}

