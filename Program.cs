using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.Internal;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CShark_lab10
{
    internal class Program
    {
        private static  HttpClient httpClient = new();
        private const   string     file = @"C:\Users\nikpop\Desktop\ticker.txt";
        private static  string?    apiKey; // from console
        public  static  string?    pswd;
        private static  DateOnly   dateFrom = new(2025, 10, 1);
        private static  DateOnly   dateTo = new(2025, 10, 4);

        static async Task Main(string[] args)
        {
            apiKey = args[0];
            pswd = args[1];
            using StreamReader reader = new(file);
            object locker = new object();
            //using (ApplicationContext context = new ApplicationContext()) { context.Database.EnsureDeleted(); context.SaveChanges(); }

            int count = 0;
            var tasks = new List<Task>();
            await foreach (var s in GetTickerFromFile(file, reader))
            {
                if (count % 20 == 0) Thread.Sleep(1000);
                tasks.Add(SetTickerAndFriendsToDB(httpClient, s, locker));
                count++;
            }
            await Task.WhenAll(tasks);

            string? userTicker = Console.ReadLine();
            PrintTickerInfo(userTicker);
        }

        static void PrintTickerInfo(string? userTicker) // вывод тикера в консоль
        {
            if(userTicker == null) return;
            using ApplicationContext context = new ApplicationContext();
            var ticker = context.Tickers.
                            Include(p => p.Prices).
                            Include(t => t.TodaysCondition).
                            Where(t => t.Ticker == userTicker).
                            FirstOrDefault();
            //for(int i = 0; i < ticker.Prices.Count; i++)
            //{
            //    Console.WriteLine($"{ticker.Prices[i].Price}\t{ticker.TodaysCondition.State}");
            //}
            Console.WriteLine($"{ticker.Prices.Last()} {ticker.TodaysCondition.State}");
            //Console.WriteLine();
        }
        static async Task SetTickerAndFriendsToDB(HttpClient httpClient, string readedTicker, object locker)
        {
            string url = $"https://api.marketdata.app/v1/stocks/candles/D/" +
                $"{readedTicker}/?from={dateFrom:o}&to={dateTo:o}&token={apiKey}";
            try
            {
                string json = await httpClient.GetStringAsync(url); // получаем цены

                PricesDeserializer? deserializedPrices = JsonSerializer.Deserialize<PricesDeserializer>(json); // массив цен
                if (deserializedPrices is null
                    || deserializedPrices.Prices is null
                    || deserializedPrices.Prices.Length == 0)
                {
                    throw new Exception("Нулевой тикер");
                }
                lock (locker)
                {
                    using ApplicationContext context = new ApplicationContext();
                    if (!context.Database.CanConnect()) throw new Exception("Невозможно подключиться к базе");
                    Tickers ticker = CreateTickerInDB(context, readedTicker); // получаем тикер из бд или создаем
                    SetPriceToDB(context, deserializedPrices, ticker.Id);
                    SetTodaysConditionToDB(context, deserializedPrices, ticker.Id);
                    //context.SaveChanges();
                    Console.WriteLine("Таблицы записаны");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception on {readedTicker}: {ex}\n");
            }
        }
        static Tickers CreateTickerInDB(ApplicationContext context, string ticker) // получаем тикер из бд или создаем тикер
        {
            Tickers? Ticker = context.Tickers.AsNoTracking().FirstOrDefault(t => t.Ticker == ticker); // ищем тикер в бд
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

            var pricesFromDB = context.Prices.Where(p => p.TickerId == TickerId && p.Date >= date).AsNoTracking().ToList(); // список цен 
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
            try
            {
                TodaysCondition? todaysCondition = context.TodaysConditions.Where(t => t.TickerId == TickerId).AsNoTracking().FirstOrDefault();
                if (todaysCondition is null)
                {
                    int size = deserializedPrices.Prices.Length; // размер массива с ценами
                    string state;

                    if (size > 1 && deserializedPrices.Prices[size - 1] > deserializedPrices.Prices[size - 2]) { state = "Up"; }
                    else if (size > 1 && deserializedPrices.Prices[size - 1] < deserializedPrices.Prices[size - 2]) { state = "Down"; }
                    else { state = "=="; }

                    todaysCondition = new TodaysCondition { TickerId = TickerId, State = state };
                    if (context.TodaysConditions.Contains(todaysCondition)) { return; }
                    context.AddRange(todaysCondition);
                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        static async IAsyncEnumerable<string> GetTickerFromFile(string file, StreamReader reader) // асинхронная корутина для считывания файла
        {
            string? text;
            while ((text = await reader.ReadLineAsync()) != null)
            {
                yield return text;
            }
        }
    }
}