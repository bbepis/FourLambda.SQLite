namespace FourLambda.SQLite.Tests;

[TestFixture]
public class EnumTests : DBTestHarness
{
	public enum TestEnum
	{
		Value1,
		Value2,
		Value3
	}

	[StoreAsText]
	public enum StringTestEnum
	{
		Value1,
		Value2,
		Value3
	}

	public class TestObj
	{
		[PrimaryKey]
		public int Id { get; set; }

		public TestEnum Value { get; set; }

		public override string ToString() => $"[TestObj: Id={Id}, Value={Value}]";
	}

	public class StringTestObj
	{
		[PrimaryKey]
		public int Id { get; set; }

		public StringTestEnum Value { get; set; }

		public override string ToString() => $"[StringTestObj: Id={Id}, Value={Value}]";
	}

	protected override void InitializeDatabase()
	{
		Database.CreateTable<TestObj>();
		Database.CreateTable<StringTestObj>();
		Database.CreateTable<ByteTestObj>();
	}

	[Test]
	public void ShouldPersistAndReadEnum()
	{
		var obj1 = new TestObj { Id = 1, Value = TestEnum.Value2 };
		var obj2 = new TestObj { Id = 2, Value = TestEnum.Value3 };

		var numIn1 = Database.Insert(obj1);
		var numIn2 = Database.Insert(obj2);
		Assert.AreEqual(1, numIn1);
		Assert.AreEqual(1, numIn2);

		var result = Database.Query<TestObj>("select * from TestObj").ToList();
		Assert.AreEqual(2, result.Count);
		Assert.AreEqual(obj1.Value, result[0].Value);
		Assert.AreEqual(obj2.Value, result[1].Value);

		Assert.AreEqual(obj1.Id, result[0].Id);
		Assert.AreEqual(obj2.Id, result[1].Id);
	}

	[Test]
	public void ShouldPersistAndReadStringEnum()
	{
		var obj1 = new StringTestObj { Id = 1, Value = StringTestEnum.Value2 };
		var obj2 = new StringTestObj { Id = 2, Value = StringTestEnum.Value3 };

		var numIn1 = Database.Insert(obj1);
		var numIn2 = Database.Insert(obj2);
		Assert.AreEqual(1, numIn1);
		Assert.AreEqual(1, numIn2);

		var result = Database.Query<StringTestObj>("select * from StringTestObj").ToList();
		Assert.AreEqual(2, result.Count);
		Assert.AreEqual(obj1.Value, result[0].Value);
		Assert.AreEqual(obj2.Value, result[1].Value);

		Assert.AreEqual(obj1.Id, result[0].Id);
		Assert.AreEqual(obj2.Id, result[1].Id);
	}

	public enum ByteTestEnum : byte
	{
		Value1 = 1,
		Value2 = 2,
		Value3 = 3
	}

	public class ByteTestObj
	{
		[PrimaryKey]
		public int Id { get; set; }

		public ByteTestEnum Value { get; set; }

		public override string ToString () => $"[ByteTestObj: Id={Id}, Value={Value}]";
	}

	[Test]
	public void Issue33_ShouldPersistAndReadByteEnum()
	{
		var obj1 = new ByteTestObj { Id = 1, Value = ByteTestEnum.Value2 };
		var obj2 = new ByteTestObj { Id = 2, Value = ByteTestEnum.Value3 };

		var numIn1 = Database.Insert(obj1);
		var numIn2 = Database.Insert(obj2);
		Assert.AreEqual(1, numIn1);
		Assert.AreEqual(1, numIn2);

		var result = Database.Query<ByteTestObj>("select * from ByteTestObj order by Id").ToList();
		Assert.AreEqual(2, result.Count);
		Assert.AreEqual(obj1.Value, result[0].Value);
		Assert.AreEqual(obj2.Value, result[1].Value);

		Assert.AreEqual(obj1.Id, result[0].Id);
		Assert.AreEqual(obj2.Id, result[1].Id);
	}
}