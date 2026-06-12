namespace FourLambda.SQLite.Tests;

[TestFixture]
public class DateTimeOffsetTest : DBTestHarness
{
	const string TestFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK";

	abstract class BaseDateTimeOffsetClass
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		public abstract DateTimeOffset ModifiedTime { get; set; }
	}

	class DateTimeOffsetAsTicksClass : BaseDateTimeOffsetClass
	{
		public override DateTimeOffset ModifiedTime { get; set; }
	}

	class DateTimeOffsetAsStringClass : BaseDateTimeOffsetClass
	{
		[StoreAsText]
		public override DateTimeOffset ModifiedTime { get; set; }
	}

	class DateTimeOffsetAsStringFormattedClass : BaseDateTimeOffsetClass
	{
		[StoreAsText(Format = TestFormat)]
		public override DateTimeOffset ModifiedTime { get; set; }
	}

	private static readonly DateTimeOffset TestDateTimeOffset = new DateTimeOffset(2012, 1, 14, 3, 2, 1, 234, TimeSpan.FromHours(2));

	[Test]
	public void AsTicks()
	{
		TestWrite<DateTimeOffsetAsTicksClass>(TestDateTimeOffset, TestDateTimeOffset.Ticks.ToString());
	}

	[Test]
	public void AsString()
	{
		TestWrite<DateTimeOffsetAsStringClass>(TestDateTimeOffset, TestDateTimeOffset.ToString("o"));
	}

	[Test]
	public void AsCustomFormattedString()
	{
		TestWrite<DateTimeOffsetAsStringFormattedClass>(TestDateTimeOffset, TestDateTimeOffset.ToString(TestFormat));
	}

	void TestWrite<T>(DateTimeOffset dateTime, string expected) where T : BaseDateTimeOffsetClass, new()
	{
		Database.CreateTable<T>();

		var o = new T
		{
			ModifiedTime = dateTime,
		};
		Database.Insert(o);
		var o2 = Database.Get<T>(o.Id);
		Assert.AreEqual(o.ModifiedTime, o2.ModifiedTime);

		var stored = Database.ExecuteScalar<string>("SELECT ModifiedTime FROM TestObj;");
		Assert.AreEqual(expected, stored);
	}
}