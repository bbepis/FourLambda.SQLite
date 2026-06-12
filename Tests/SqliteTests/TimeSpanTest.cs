namespace FourLambda.SQLite.Tests;

[TestFixture]
public class TimeSpanTest : DBTestHarness
{
	private const string TestFormat = @"dddddd\.hh\:mm\:ss\.fffffff";

	private abstract class BaseTimeSpanClass
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		public abstract TimeSpan Elapsed { get; set; }
	}

	[Table("TestObj")]
	private class TimeSpanAsTicksClass : BaseTimeSpanClass
	{
		public override TimeSpan Elapsed { get; set; }
	}

	[Table("TestObj")]
	private class TimeSpanAsStringClass : BaseTimeSpanClass
	{
		[StoreAsText]
		public override TimeSpan Elapsed { get; set; }
	}

	[Table("TestObj")]
	private class TimeSpanAsStringFormattedClass : BaseTimeSpanClass
	{
		[StoreAsText(Format = TestFormat)]
		public override TimeSpan Elapsed { get; set; }
	}

	private static readonly TimeSpan TestTimeSpan = new(42, 12, 33, 20, 501);

	[Test]
	public void AsTicks()
	{
		TestWrite<TimeSpanAsTicksClass>(TestTimeSpan, TestTimeSpan.Ticks.ToString());
	}

	[Test]
	public void AsString()
	{
		TestWrite<TimeSpanAsStringClass>(TestTimeSpan, TestTimeSpan.ToString("c"));
	}

	[Test]
	public void AsCustomFormattedString()
	{
		TestWrite<TimeSpanAsStringFormattedClass>(TestTimeSpan, TestTimeSpan.ToString(TestFormat));
	}

	private void TestWrite<T>(TimeSpan dateTime, string expected) where T : BaseTimeSpanClass, new()
	{
		Database.CreateTable<T>();

		var o = new T
		{
			Elapsed = dateTime
		};
		Database.Insert(o);

		var o2 = Database.Get<T>(o.Id);
		Assert.AreEqual(o.Elapsed, o2.Elapsed);

		var stored = Database.ExecuteScalar<string>("SELECT Elapsed FROM TestObj;");
		Assert.AreEqual(expected, stored);
	}
}