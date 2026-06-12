using System.Threading.Tasks;

#if NETFX_CORE
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using SetUp = Microsoft.VisualStudio.TestPlatform.UnitTestFramework.TestInitializeAttribute;
using TestFixture = Microsoft.VisualStudio.TestPlatform.UnitTestFramework.TestClassAttribute;
using Test = Microsoft.VisualStudio.TestPlatform.UnitTestFramework.TestMethodAttribute;
#else
#endif

namespace FourLambda.SQLite.Tests;

[TestFixture]
public class TimeSpanTest : DBTestHarness
{
	const string TestFormat = "hh':'mm':'ss";

	abstract class BaseTimeSpanClass
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		public abstract TimeSpan Elapsed { get; set; }
	}

	class TimeSpanAsTicksClass : BaseTimeSpanClass
	{
		public override TimeSpan Elapsed { get; set; }
	}

	class TimeSpanAsStringClass : BaseTimeSpanClass
	{
		[StoreAsText]
		public override TimeSpan Elapsed { get; set; }
	}

	class TimeSpanAsStringFormattedClass : BaseTimeSpanClass
	{
		[StoreAsText(Format = TestFormat)]
		public override TimeSpan Elapsed { get; set; }
	}

	private static readonly TimeSpan TestTimeSpan = new TimeSpan(42, 12, 33, 20, 501);

	[Test]
	public void AsTicks()
	{
		TestWrite<TimeSpanAsTicksClass>(TestTimeSpan, TestTimeSpan.Ticks.ToString());
	}

	[Test]
	public void AsString()
	{
		TestWrite<TimeSpanAsStringClass>(TestTimeSpan, TestTimeSpan.ToString("o"));
	}

	[Test]
	public void AsCustomFormattedString()
	{
		TestWrite<TimeSpanAsStringFormattedClass>(TestTimeSpan, TestTimeSpan.ToString(TestFormat));
	}

	void TestWrite<T>(TimeSpan dateTime, string expected) where T : BaseTimeSpanClass, new()
	{
		Database.CreateTable<T>();

		var o = new T
		{
			Elapsed = dateTime,
		};
		Database.Insert(o);
		var o2 = Database.Get<T>(o.Id);
		Assert.AreEqual(o.Elapsed, o2.Elapsed);

		var stored = Database.ExecuteScalar<string>("SELECT Elapsed FROM TestObj;");
		Assert.AreEqual(expected, stored);
	}
}