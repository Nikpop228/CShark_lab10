using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CShark_lab10
{
    internal class Program
    {
        static HttpClient httpClient = new();
        const string file = @"C:\Users\nikpop\Desktop\ticker.txt";
        static string? apiKey; // from console
        public static string? pswd;
        static DateOnly dateFrom = new(2025, 10, 1);
        static DateOnly dateTo = new(2025, 10, 4);

        static async Task Main(string[] args)
        {
            apiKey = args[0];//Console.ReadLine();
            pswd = args[1];//Console.ReadLine();

            //using (ApplicationContext context = new ApplicationContext()) { context.SaveChanges(); }

            await foreach (var s in GetTickerFromFile(file))
            {
                await SetTickerToDB(httpClient, s);
            }

            string? userTicker = Console.ReadLine();
            PrintTickerInfo(userTicker);
        }

        static void PrintTickerInfo(string? userTicker) // вывод тикера в консоль
        {
            if(userTicker == null) return;
            using ApplicationContext context = new ApplicationContext();
            var ticker = context.Tickers.Include(p => p.Prices).Include(t => t.TodaysCondition).Where(t => t.Ticker == userTicker).FirstOrDefault();
            for(int i = 0; i < ticker.Prices.Count; i++)
            {
                Console.WriteLine($"{ticker.Prices[i].Price}\t{ticker.TodaysCondition.State}");
            }
            Console.WriteLine();
        }
        static async Task SetTickerToDB(HttpClient httpClient, string readedTicker)
        {
            string url = $"https://api.marketdata.app/v1/stocks/candles/D/{readedTicker}/?from={dateFrom:o}&to={dateTo:o}&token={apiKey}";
            try
            {
                using ApplicationContext context = new ApplicationContext();
                string json = await httpClient.GetStringAsync(url); // получаем цены

                PricesDeserializer? deserializedPrices = JsonSerializer.Deserialize<PricesDeserializer>(json); // массив цен
                if (deserializedPrices is null || deserializedPrices.Prices is null || deserializedPrices.Prices.Length == 0) throw new Exception();
                
                Tickers ticker = GetTickerFromDB(context, readedTicker); // получаем тикер из бд или создаем
                SetPriceToDB(context, deserializedPrices, ticker.Id);
                SetTodaysConditionToDB(context, deserializedPrices, ticker.Id);
            }
            catch (Exception)
            {
                Console.WriteLine($"Exception on {readedTicker}: 404\n");
            }

        }
        static Tickers GetTickerFromDB(ApplicationContext context, string ticker) // получаем тикер из бд или создаем тикер
        {
            Tickers? Ticker = context.Tickers/*.Include(t => t.Prices).Include(t => t.TodaysCondition)*/.FirstOrDefault(t => t.Ticker == ticker); // ищем тикер в бд
            if (Ticker is null)
            {
                Ticker = new Tickers { Ticker = ticker }; // если не находим - сощдаем новый
                context.Add(Ticker);
                context.SaveChanges();
            }
            return Ticker;
        }
        static void SetPriceToDB(ApplicationContext context, PricesDeserializer deserializedPrices, int TickerId)
        {
            int size = deserializedPrices.Prices.Length; // размер массива с ценами
            List<Prices> prices = new(size);
            DateOnly date = dateFrom;

            var pricesFromDB = context.Prices.Where(p => p.TickerId == TickerId && p.Date >= date).ToList(); // список цен 
            var comparer = new PricesComparer();
            for (int i = 0; i < size; i++)
            {
                var newPrice = new Prices { TickerId = TickerId, Price = deserializedPrices.Prices[i], Date = date };
                date = date.AddDays(1); // увеличиваем дату на день
                if (pricesFromDB.Count > 0 && pricesFromDB.Contains(newPrice, comparer)) continue; // если цена существует, не ддобаляем ничего
                prices.Add(newPrice);
            }

            context.AddRange(prices);
            context.SaveChanges();
        }
        static void SetTodaysConditionToDB(ApplicationContext context, PricesDeserializer deserializedPrices, int TickerId)
        {
            var todaysCondition = context.TodaysConditions.Where(t => t.TickerId == TickerId).FirstOrDefault();
            if (todaysCondition is null)
            {
                int size = deserializedPrices.Prices.Length; // размер массива с ценами
                string state;

                if (deserializedPrices.Prices[size - 1] > deserializedPrices.Prices[size - 2]) { state = "Up"; }
                else if (deserializedPrices.Prices[size - 1] == deserializedPrices.Prices[size - 2]) { state = "=="; }
                else { state = "Down"; }

                todaysCondition = new TodaysCondition { TickerId = TickerId, State = state };
                if (context.TodaysConditions.Contains(todaysCondition)) { return; }
                context.AddRange(todaysCondition);
                context.SaveChanges();
            }
        }
        static async IAsyncEnumerable<string> GetTickerFromFile(string file) // асинхронная корутина для считывания файла
        {
            using StreamReader reader = new(file);
            string? text;
            while ((text = await reader.ReadLineAsync()) != null)
            {
                yield return text;
            }
        }
    }
}