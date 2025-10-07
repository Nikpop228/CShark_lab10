using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CShark_lab10
{
    internal class ApplicationContext : DbContext
    {
        public DbSet<Tickers> Tickers { get; set; }
        public DbSet<Prices> Prices { get; set; }
        public DbSet<TodaysCondition> TodaysConditions { get; set; }
        public ApplicationContext()
        {
            //Database.EnsureDeleted();
            Database.EnsureCreated();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql($"Host=localhost;Port=5433;Database=tickersdb;Username=postgres;Password={Program.pswd}");
        }
    }

    internal class Tickers
    {
        public int Id { get; set; }
        public string? Ticker { get; set; }
        public TodaysCondition? TodaysCondition { get; set; }
        //[JsonPropertyName("c")]
        public List<Prices> Prices { get; set; } = new();
    }

    internal class TodaysCondition
    {
        public int Id { get; set; }
        public int TickerId { get; set; }
        public string? State { get; set; }
        public Tickers? Ticker { get; set; }
    }
    internal class PricesDeserializer // вспомогательный классс для десериализации цен
    {
        [JsonPropertyName("c")]
        public double[]? Prices { get; set; }
    }
    internal class Prices
    {
        public int Id { get; set; }
        public int TickerId { get; set; }
        public double? Price { get; set; }
        public DateOnly? Date { get; set; }
        public Tickers? Ticker { get; set; }
        public bool Equals(Prices? y)
        {
            if(y is Prices) return TickerId == y.TickerId && Price == y.Price && Date == y.Date;
            return false;
        }
    }
    internal class PricesComparer : IEqualityComparer<Prices>
    {
        // Products are equal if their names and product numbers are equal.
        public bool Equals(Prices x, Prices y)
        {

            //Check whether the compared objects reference the same data.
            if (Object.ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            //Check whether the products' properties are equal.
            return x.TickerId == y.TickerId && x.Price == y.Price && x.Date == y.Date;
        }

        // If Equals() returns true for a pair of objects
        // then GetHashCode() must return the same value for these objects.

        public int GetHashCode(Prices product)
        {
            //Check whether the object is null
            if (Object.ReferenceEquals(product, null)) return 0;

            //Get hash code for the Name field if it is not null.
            int hashPrice = product.Price == null ? 0 : product.Price.GetHashCode();

            //Get hash code for the Code field.
            int hashTickerId = product.TickerId.GetHashCode();

            //Calculate the hash code for the product.
            return hashPrice ^ hashTickerId;
        }
    }
}
