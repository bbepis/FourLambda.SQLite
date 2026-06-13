using System.Linq.Expressions;

namespace FourLambda.SQLite.Tests.Temporal;

public abstract class BaseTemporalTest<TTemporalType> : DBTestHarness where TTemporalType : struct, IFormattable, IComparable<TTemporalType>
{
	protected abstract string TestDefaultFormat { get; }
	protected abstract string TestCustomFormat { get; }
	protected abstract TTemporalType TestedValue { get; }
	protected abstract long TestedValueTicks { get; }

	protected abstract TTemporalType EpochValue { get; }
	protected abstract TTemporalType?[] NullableTestValues { get; }

	[Table("TestObj", Strict = true)]
	private class TemporalClass
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; init; }

		public TTemporalType? ModifiedTime { get; set; }
	}

	[Test]
	public void TestWriteAsTicks()
	{
		TestWrite(new TableMapping(typeof(TemporalClass)), TestedValueTicks.ToString());
	}

	[Test]
	public void TestWriteAsString()
	{
		var stringMapping = new TableMapping(typeof(TemporalClass));
		var column = stringMapping.Columns.First(x => x.Name == nameof(TemporalClass.ModifiedTime));
		column.StoreAsText = true;

		TestWrite(stringMapping, TestedValue.ToString(TestDefaultFormat, null));
	}

	[Test]
	public void TestWriteAsCustomFormattedString()
	{
		var customStringMapping = new TableMapping(typeof(TemporalClass));
		var column = customStringMapping.Columns.First(x => x.Name == nameof(TemporalClass.ModifiedTime));
		column.StoreAsText = true;
		column.StoreAsTextFormat = TestCustomFormat;

		TestWrite(customStringMapping, TestedValue.ToString(TestCustomFormat, null));
	}

	private void TestWrite(TableMapping mapping, string expected)
	{
		if (mapping.Columns[1].StoreAsTextFormat == "custom")
			mapping.Columns[1].StoreAsTextFormat = TestCustomFormat;

		Database.CreateTable(mapping);

		var o = new TemporalClass
		{
			ModifiedTime = TestedValue
		};

		Database.Insert(o, "", mapping);

		var o2 = Database.Get<TemporalClass>(o.Id, mapping);
		Assert.AreEqual(o.ModifiedTime, o2.ModifiedTime);

		var stored = Database.ExecuteScalar<string>("SELECT ModifiedTime FROM TestObj;");
		Assert.AreEqual(expected, stored);
	}

	private Expression<Func<TemporalClass, bool>> BuildPredicate(bool isGreaterThan)
	{
		var param = Expression.Parameter(typeof(TemporalClass), "x");
		var prop = Expression.Property(param, nameof(TemporalClass.ModifiedTime)); // x.ModifiedTime

		var constant = Expression.Constant(EpochValue, typeof(TTemporalType));

		var binaryExpression = isGreaterThan
			? Expression.GreaterThan(prop, constant)
			: Expression.Equal(prop, constant);

		return Expression.Lambda<Func<TemporalClass, bool>>(binaryExpression, param);
	}

	private void InternalLinqNullableTest(TableMapping map)
	{
		Database.CreateTable(map);

		foreach (var time in NullableTestValues)
			Database.Insert(new TemporalClass { ModifiedTime = time });

		var stored = Database.QueryScalars<string>("SELECT ModifiedTime FROM TestObj;").ToList();

		// workaround for not being able to use == or > expressions in generic parameter types

		// x => x.ModifiedTime == EpochValue
		var res = Database.Table<TemporalClass>().Where(BuildPredicate(false)).ToList();
		Assert.AreEqual(1, res.Count);

		// x => x.ModifiedTime > EpochValue
		res = Database.Table<TemporalClass>().Where(BuildPredicate(true)).ToList();
		Assert.AreEqual(2, res.Count);
	}

	//[Test]
	//public void LinqNullableAsTicks()
	//{
	//	InternalLinqNullableTest<TemporalAsTicksClass>();
	//}

	//[Test]
	//public void LinqNullableAsString()
	//{
	//	InternalLinqNullableTest<TemporalAsStringClass>();
	//}

	//[Test]
	//public void LinqNullableAsFormattedString()
	//{
	//	InternalLinqNullableTest<TemporalAsStringFormattedClass>();
	//}
}

[TestFixture]
public class DateTimeTest : BaseTemporalTest<DateTime>
{
	protected override string TestDefaultFormat => "o";
	protected override string TestCustomFormat => "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff";
	protected override DateTime TestedValue => new DateTime(2012, 1, 14, 3, 2, 1, 234);
	protected override long TestedValueTicks => TestedValue.Ticks;
	protected override DateTime EpochValue => new DateTime(1970, 1, 1);

	protected override DateTime?[] NullableTestValues =>
	[
		EpochValue,
		new DateTime(1980, 7, 23),
		null,
		new DateTime(2019, 1, 23)
	];
}

[TestFixture]
public class DateTimeOffsetTest : BaseTemporalTest<DateTimeOffset>
{
	protected override string TestDefaultFormat => "o";
	protected override string TestCustomFormat => "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK";
	protected override DateTimeOffset TestedValue => new(2012, 1, 14, 3, 2, 1, 234, TimeSpan.FromHours(2));
	protected override long TestedValueTicks => TestedValue.UtcTicks;
	protected override DateTimeOffset EpochValue => DateTimeOffset.UnixEpoch;

	protected override DateTimeOffset?[] NullableTestValues =>
	[
		EpochValue,
		new DateTime(1980, 7, 23),
		null,
		new DateTime(2019, 1, 23)
	];
}

[TestFixture]
public class TimeSpanTest : BaseTemporalTest<TimeSpan>
{
	protected override string TestDefaultFormat => "c";
	protected override string TestCustomFormat => @"dddddd\.hh\:mm\:ss\.fffffff";
	protected override TimeSpan TestedValue => new(42, 12, 33, 20, 501);
	protected override long TestedValueTicks => TestedValue.Ticks;
	protected override TimeSpan EpochValue => TimeSpan.Zero;

	protected override TimeSpan?[] NullableTestValues =>
	[
		EpochValue,
		TimeSpan.FromHours(2),
		null,
		TimeSpan.FromDays(2),
	];
}

[TestFixture]
public class DateOnlyTest : BaseTemporalTest<DateOnly>
{
	protected override string TestDefaultFormat => "o";
	protected override string TestCustomFormat => @"yy.mm.dd";
	protected override DateOnly TestedValue => new(2012, 1, 14);
	protected override long TestedValueTicks => TestedValue.ToDateTime(TimeOnly.MinValue).Ticks;
	protected override DateOnly EpochValue => new DateOnly(1970, 1, 1);

	protected override DateOnly?[] NullableTestValues =>
	[
		EpochValue,
		new DateOnly(1980, 7, 23),
		null,
		new DateOnly(2019, 1, 23)
	];
}

[TestFixture]
public class TimeOnlyTest : BaseTemporalTest<TimeOnly>
{
	protected override string TestDefaultFormat => "o";
	protected override string TestCustomFormat => @"hh.mm.ss";
	protected override TimeOnly TestedValue => new(15, 1, 14);
	protected override long TestedValueTicks => TestedValue.Ticks;
	protected override TimeOnly EpochValue => TimeOnly.MinValue;

	protected override TimeOnly?[] NullableTestValues =>
	[
		EpochValue,
		new TimeOnly(1, 2, 3),
		null,
		new TimeOnly(4, 5, 6)
	];
}