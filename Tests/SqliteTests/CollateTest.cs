using System.Diagnostics.CodeAnalysis;

namespace FourLambda.SQLite.Tests;

[TestFixture]
public class CollateTest : DBTestHarness
{
	public class TestObj
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }

		public string CollateDefault { get; set; }

		[Collation("BINARY")]
		public string CollateBinary { get; set; }

		[Collation("RTRIM")]
		public string CollateRTrim { get; set; }

		[Collation("NOCASE")]
		public string CollateNoCase { get; set; }


		[ExcludeFromCodeCoverage]
		public override string ToString() => $"[TestObj: Id={Id}]";
	}

	protected override void InitializeDatabase()
	{
		Database.CreateTable<TestObj>();
	}

	[Test]
	public void Collate()
	{
		var obj = new TestObj
		{
			CollateDefault = "Alpha ",
			CollateBinary = "Alpha ",
			CollateRTrim = "Alpha ",
			CollateNoCase = "Alpha "
		};

		Database.Insert(obj);

		Assert.AreEqual(1, Database.Table<TestObj>().Count(o => o.CollateDefault == "Alpha "));
		Assert.AreEqual(0, Database.Table<TestObj>().Count(o => o.CollateDefault == "ALPHA "));
		Assert.AreEqual(0, Database.Table<TestObj>().Count(o => o.CollateDefault == "Alpha"));
		Assert.AreEqual(0, Database.Table<TestObj>().Count(o => o.CollateDefault == "ALPHA"));

		Assert.AreEqual(1, Database.Table<TestObj>().Count(o => o.CollateBinary == "Alpha "));
		Assert.AreEqual(0, Database.Table<TestObj>().Count(o => o.CollateBinary == "ALPHA "));
		Assert.AreEqual(0, Database.Table<TestObj>().Count(o => o.CollateBinary == "Alpha"));
		Assert.AreEqual(0, Database.Table<TestObj>().Count(o => o.CollateBinary == "ALPHA"));

		Assert.AreEqual(1, Database.Table<TestObj>().Count(o => o.CollateRTrim == "Alpha "));
		Assert.AreEqual(0, Database.Table<TestObj>().Count(o => o.CollateRTrim == "ALPHA "));
		Assert.AreEqual(1, Database.Table<TestObj>().Count(o => o.CollateRTrim == "Alpha"));
		Assert.AreEqual(0, Database.Table<TestObj>().Count(o => o.CollateRTrim == "ALPHA"));

		Assert.AreEqual(1, Database.Table<TestObj>().Count(o => o.CollateNoCase == "Alpha "));
		Assert.AreEqual(1, Database.Table<TestObj>().Count(o => o.CollateNoCase == "ALPHA "));
		Assert.AreEqual(0, Database.Table<TestObj>().Count(o => o.CollateNoCase == "Alpha"));
		Assert.AreEqual(0, Database.Table<TestObj>().Count(o => o.CollateNoCase == "ALPHA"));
	}
}