namespace FourLambda.SQLite.Tests;

public class ReadmeTest : DBTestHarness
{
	public class Stock
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		public string Symbol { get; set; }
	}

	public class Valuation
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		[Indexed]
		public int StockId { get; set; }

		public DateTime Time { get; set; }
		public decimal Price { get; set; }
	}

	public void AddStock(string symbol)
	{
		var stock = new Stock
		{
			Symbol = symbol
		};
		Database.Insert(stock); // Returns the number of rows added to the table
		Console.WriteLine("{0} == {1}", stock.Symbol, stock.Id);
	}

	[Test]
	public void Synchronous()
	{
		Database.CreateTable<Stock>();
		Database.CreateTable<Valuation>();

		AddStock("A1");
		AddStock("A2");
		AddStock("A3");
		AddStock("B1");
		AddStock("B2");
		AddStock("B3");

		var query = Database.Table<Stock>().Where(v => v.Symbol.StartsWith("A"));

		foreach (var stock in query)
			Console.WriteLine("Stock: " + stock.Symbol);

		Assert.AreEqual(3, query.ToList().Count);
	}

	[Test]
	public void Cipher()
	{
		var databasePath = GetDisposablePath();

		const string key = "password";
		var options = new SQLiteConnectionString(databasePath, key);
		using var encryptedDb = new SQLiteConnection(options);

		var options2 = new SQLiteConnectionString(databasePath, key)
		{
			PreKeyAction = db => db.Execute("PRAGMA cipher_default_use_hmac = OFF;"),
			PostKeyAction = db => db.Execute("PRAGMA kdf_iter = 128000;")
		};

		using var encryptedDb2 = new SQLiteConnection(options2);
	}

	[Test]
	public void Manual()
	{
		Database.Execute("create table Stock(Symbol varchar(100) not null)");
		Database.Execute("insert into Stock(Symbol) values (?)", "MSFT");
		var stocks = Database.Query<Stock>("select * from Stock");

		Assert.AreEqual(1, stocks.Count);
		Assert.AreEqual("MSFT", stocks[0].Symbol);
	}
}