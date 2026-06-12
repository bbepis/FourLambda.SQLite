namespace FourLambda.SQLite.Tests;

[TestFixture]
public class DateTimeTest : DBTestHarness
{
	private const string TestFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff";

	private abstract class BaseDateTimeClass
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		public abstract DateTime? ModifiedTime { get; set; }
	}

	private class DateTimeAsTicksClass : BaseDateTimeClass
	{
		public override DateTime? ModifiedTime { get; set; }
	}

	private class DateTimeAsStringClass : BaseDateTimeClass
	{
		[StoreAsText]
		public override DateTime? ModifiedTime { get; set; }
	}

	private class DateTimeAsStringFormattedClass : BaseDateTimeClass
	{
		[StoreAsText(Format = TestFormat)]
		public override DateTime? ModifiedTime { get; set; }
	}

	private static readonly DateTime TestDateTime = new(2012, 1, 14, 3, 2, 1, 234);

	[Test]
	public void AsTicks()
	{
		TestWrite<DateTimeAsTicksClass>(TestDateTime, TestDateTime.Ticks.ToString());
	}

	[Test]
	public void AsString()
	{
		TestWrite<DateTimeAsStringClass>(TestDateTime, TestDateTime.ToString("o"));
	}

	public void AsCustomFormattedString()
	{
		TestWrite<DateTimeAsStringFormattedClass>(TestDateTime, TestDateTime.ToString(TestFormat));
	}

	private void TestWrite<T>(DateTime dateTime, string expected) where T : BaseDateTimeClass, new()
	{
		Database.CreateTable<T>();

		var o = new T
		{
			ModifiedTime = dateTime
		};
		Database.Insert(o);
		var o2 = Database.Get<T>(o.Id);
		Assert.AreEqual(o.ModifiedTime, o2.ModifiedTime);

		var stored = Database.ExecuteScalar<string>("SELECT ModifiedTime FROM TestObj;");
		Assert.AreEqual(expected, stored);
	}


	private void InternalLinqNullableTest<T>() where T : BaseDateTimeClass, new()
	{
		Database.CreateTable<T>();

		var epochTime = new DateTime(1970, 1, 1);

		Database.Insert(new T { ModifiedTime = epochTime });
		Database.Insert(new T { ModifiedTime = new DateTime(1980, 7, 23) });
		Database.Insert(new T { ModifiedTime = null });
		Database.Insert(new T { ModifiedTime = new DateTime(2019, 1, 23) });

		var res = Database.Table<T>().Where(x => x.ModifiedTime == epochTime).ToList();
		Assert.AreEqual(1, res.Count);

		res = Database.Table<T>().Where(x => x.ModifiedTime > epochTime).ToList();
		Assert.AreEqual(2, res.Count);
	}

	[Test]
	public void LinqNullableAsTicks()
	{
		InternalLinqNullableTest<DateTimeAsTicksClass>();
	}

	[Test]
	public void LinqNullableAsString()
	{
		InternalLinqNullableTest<DateTimeAsStringClass>();
	}

	[Test]
	public void LinqNullableAsFormattedString()
	{
		InternalLinqNullableTest<DateTimeAsStringFormattedClass>();
	}
}