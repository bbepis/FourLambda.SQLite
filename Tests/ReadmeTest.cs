using System;
using System.Linq;
using System.Text;
using SQLite;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;

#if NETFX_CORE
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using SetUp = Microsoft.VisualStudio.TestPlatform.UnitTestFramework.TestInitializeAttribute;
using TestFixture = Microsoft.VisualStudio.TestPlatform.UnitTestFramework.TestClassAttribute;
using Test = Microsoft.VisualStudio.TestPlatform.UnitTestFramework.TestMethodAttribute;
#else
using NUnit.Framework;
#endif

namespace SQLite.Tests
{
	public class ReadmeTest
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

		public static void AddStock (SQLiteConnection db, string symbol)
		{
			var stock = new Stock () {
				Symbol = symbol
			};
			db.Insert (stock); // Returns the number of rows added to the table
			Console.WriteLine ("{0} == {1}", stock.Symbol, stock.Id);
		}

		public static IEnumerable<Valuation> QueryValuations (SQLiteConnection db, Stock stock)
		{
			return db.Query<Valuation> ("select * from Valuation where StockId = ?", stock.Id);
		}

		public class Val
		{
			public decimal Money { get; set; }
			public DateTime Date { get; set; }
		}

		public static IEnumerable<Val> QueryVals (SQLiteConnection db, Stock stock)
		{
			return db.Query<Val> ("select \"Price\" as \"Money\", \"Time\" as \"Date\" from Valuation where StockId = ?", stock.Id);
		}

		[Test]
		public void Synchronous ()
		{
			var databasePath = Path.Combine (Path.GetTempPath(), "SynchronousMyData"+ Guid.NewGuid() +".db");
			File.Delete(databasePath);
			var db = new SQLiteConnection (databasePath);
			try
			{
				db.CreateTable<Stock>();
				db.CreateTable<Valuation>();

				AddStock(db, "A1");
				AddStock(db, "A2");
				AddStock(db, "A3");
				AddStock(db, "B1");
				AddStock(db, "B2");
				AddStock(db, "B3");

				var query = db.Table<Stock>().Where(v => v.Symbol.StartsWith("A"));

				foreach (var stock in query)
					Console.WriteLine("Stock: " + stock.Symbol);

				Assert.AreEqual(3, query.ToList().Count);
			}
			finally
			{
				db.Close();
				File.Delete(databasePath);
			}


		}

		[Test]
		public void Cipher()
		{
			var databasePath = Path.Combine(Path.GetTempPath(), "CipherMyData" + Guid.NewGuid() + ".db");
			File.Delete(databasePath);

			var options = new SQLiteConnectionString(databasePath, key: "password");
			var encryptedDb = new SQLiteConnection(options);

			var options2 = new SQLiteConnectionString(databasePath,
				key: "password",
				preKeyAction: db => db.Execute("PRAGMA cipher_default_use_hmac = OFF;"),
				postKeyAction: db => db.Execute("PRAGMA kdf_iter = 128000;"));
			SQLiteConnection encryptedDb2 = null;
			try
			{
				encryptedDb2 = new SQLiteConnection(options2);
			}
			finally
			{
				encryptedDb2?.Close();
				File.Delete(databasePath);
			}
		}

		[Test]
		public void Manual()
		{
			var db = new SQLiteConnection (":memory:");
			try
			{

				db.Execute("create table Stock(Symbol varchar(100) not null)");
				db.Execute("insert into Stock(Symbol) values (?)", "MSFT");
				var stocks = db.Query<Stock>("select * from Stock");

				Assert.AreEqual(1, stocks.Count);
				Assert.AreEqual("MSFT", stocks[0].Symbol);
			}
			finally
			{
				db.Close();
			}
		}
	}
}
