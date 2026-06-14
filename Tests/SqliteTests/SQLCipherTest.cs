namespace FourLambda.SQLite.Tests;

#if EncryptionEnabled
[TestFixture]
public class SQLCipherTest : DBTestHarness
{
	private class TestTable
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		public string Value { get; set; }
	}

	[SetUp]
	public void Setup()
	{
		// open an in memory connection and reset SQLCipher default pragma settings
		using (var c = new SQLiteConnection(":memory:"))
		{
			c.Execute("PRAGMA cipher_default_use_hmac = ON;");
		}
	}

	[Test]
	public void SetStringKey()
	{
		var path = GetDisposablePath();

		var key = "SecretPassword";

		using (var db = new SQLiteConnection(new SQLiteConnectionString(path, key)))
		{
			db.CreateTable<TestTable>();
			db.Insert(new TestTable { Value = "Hello" });
		}

		using (var db = new SQLiteConnection(new SQLiteConnectionString(path, key)))
		{
			var r = db.Table<TestTable>().First();

			Assert.AreEqual("Hello", r.Value);
		}
	}

	[Test]
	public void SetBytesKey()
	{
		var path = GetDisposablePath();

		var key = new byte[32];
		Random.Shared.NextBytes(key);

		using (var db = new SQLiteConnection(new SQLiteConnectionString(path, key)))
		{
			db.CreateTable<TestTable>();
			db.Insert(new TestTable { Value = "Hello" });
		}

		using (var db = new SQLiteConnection(new SQLiteConnectionString(path, key)))
		{
			var r = db.Table<TestTable>().First();

			Assert.AreEqual("Hello", r.Value);
		}
	}

	[Test]
	public void SetEmptyStringKey()
	{
		using var db = new SQLiteConnection(new SQLiteConnectionString(GetDisposablePath(), ""));
	}

	[Test]
	public void SetBadBytesKey()
	{
		Assert.Throws<ArgumentException>(() =>
		{
			using var db =
				new SQLiteConnection(new SQLiteConnectionString(GetDisposablePath(), new byte[] { 1, 2, 3, 4 }));
		});
	}

	[Test]
	public void SetPreKeyAction()
	{
		var path = GetDisposablePath();
		var key = "SecretKey";

		using var db = new SQLiteConnection(new SQLiteConnectionString(path, key)
		{
			PreKeyAction = conn => conn.Execute("PRAGMA page_size = 8192;")
		});

		db.CreateTable<TestTable>();
		db.Insert(new TestTable { Value = "Secret Value" });
		Assert.AreEqual("8192", db.ExecuteScalar<string>("PRAGMA page_size;"));
	}

	[Test]
	public void SetPostKeyAction()
	{
		var path = GetDisposablePath();
		var key = "SecretKey";

		using var db = new SQLiteConnection(new SQLiteConnectionString(path, key)
		{
			PostKeyAction = conn => conn.Execute("PRAGMA page_size = 512;")
		});

		db.CreateTable<TestTable>();
		db.Insert(new TestTable { Value = "Secret Value" });
		Assert.AreEqual("512", db.ExecuteScalar<string>("PRAGMA page_size;"));
	}

	[Test]
	public void CheckJournalModeForNonKeyed()
	{
		using var db = new SQLiteConnection(GetDisposablePath());
		db.CreateTable<TestTable>();
		db.EnableWriteAheadLogging();

		Assert.AreEqual("wal", db.ExecuteScalar<string>("PRAGMA journal_mode;"));
	}

	[Test]
	public void ResetStringKey()
	{
		var path = GetDisposablePath();

		var originalKey = "SecretPassword";
		var newKey = "SecretKey";

		using (var db = new SQLiteConnection(new SQLiteConnectionString(path, originalKey)))
		{
			db.ReKey(newKey);
			path = db.DatabasePath;

			db.CreateTable<TestTable>();
			db.Insert(new TestTable { Value = "Hello" });
		}

		using (var db = new SQLiteConnection(new SQLiteConnectionString(path, newKey)))
		{
			var r = db.Table<TestTable>().First();

			Assert.AreEqual("Hello", r.Value);
		}
	}

	[Test]
	public void ResetByteKey()
	{
		var path = GetDisposablePath();

		var originalKey = new byte[32];
		Random.Shared.NextBytes(originalKey);
		var newKey = new byte[32];
		Random.Shared.NextBytes(newKey);

		using (var db = new SQLiteConnection(new SQLiteConnectionString(path, originalKey)))
		{
			db.ReKey(newKey);
			path = db.DatabasePath;

			db.CreateTable<TestTable>();
			db.Insert(new TestTable { Value = "Hello" });
		}

		using (var db = new SQLiteConnection(new SQLiteConnectionString(path, newKey)))
		{
			var r = db.Table<TestTable>().First();

			Assert.AreEqual("Hello", r.Value);
		}
	}

	[Test]
	public void ResetBadKey()
	{
		var key = new byte[] { 42 };

		Assert.Throws<ArgumentException>(() => { Database.ReKey(key); });
	}
}
#endif