using System.Diagnostics.CodeAnalysis;

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

	public enum TestLongEnum : long
	{
		Value1,
		Value2,
		Value3
	}

	public enum TestByteEnum : byte
	{
		Value1,
		Value2,
		Value3
	}

	public class TestObj
	{
		[PrimaryKey]
		public int Id { get; set; }

		public TestEnum EnumValue { get; set; }
		public TestLongEnum LongEnumValue { get; set; }
		public TestByteEnum ByteEnumValue { get; set; }


		[ExcludeFromCodeCoverage]
		public override string ToString() => $"[TestObj: {nameof(Id)}={Id}, {nameof(EnumValue)}={EnumValue}, {nameof(LongEnumValue)}={LongEnumValue}, {nameof(ByteEnumValue)}={ByteEnumValue}]";
	}

	public class StringTestObj
	{
		[PrimaryKey]
		public int Id { get; set; }

		[StoreAsText]
		public TestEnum EnumValue { get; set; }

		[StoreAsText]
		public TestLongEnum LongEnumValue { get; set; }

		[StoreAsText]
		public TestByteEnum ByteEnumValue { get; set; }


		[ExcludeFromCodeCoverage]
		public override string ToString() => $"[TestObj: {nameof(Id)}={Id}, {nameof(EnumValue)}={EnumValue}, {nameof(LongEnumValue)}={LongEnumValue}, {nameof(ByteEnumValue)}={ByteEnumValue}]";
	}

	protected override void InitializeDatabase()
	{
		Database.CreateTable<TestObj>();
		Database.CreateTable<StringTestObj>();
	}

	[Test]
	public void ShouldPersistAndReadEnum()
	{
		var testObjects = new[]
		{
			new TestObj { Id = 1, EnumValue = TestEnum.Value2, LongEnumValue = TestLongEnum.Value2, ByteEnumValue = TestByteEnum.Value3 },
			new TestObj { Id = 2, EnumValue = TestEnum.Value3, LongEnumValue = TestLongEnum.Value2, ByteEnumValue = TestByteEnum.Value3 },
		};

		foreach (var item in testObjects)
			Assert.AreEqual(1, Database.Insert(item));

		var result = Database.Table<TestObj>().ToList();
		Assert.AreEqual(2, testObjects.Length);

		for (var i = 0; i < testObjects.Length; i++)
		{
			var item = testObjects[i];

			Assert.AreEqual(item.Id, result[i].Id);
			Assert.AreEqual(item.EnumValue, result[i].EnumValue);
			Assert.AreEqual(item.LongEnumValue, result[i].LongEnumValue);
			Assert.AreEqual(item.ByteEnumValue, result[i].ByteEnumValue);
		}
	}

	[Test]
	public void ShouldPersistAndReadStringEnum()
	{
		var testObjects = new[]
		{
			new TestObj { Id = 1, EnumValue = TestEnum.Value2, LongEnumValue = TestLongEnum.Value2, ByteEnumValue = TestByteEnum.Value3 },
			new TestObj { Id = 2, EnumValue = TestEnum.Value3, LongEnumValue = TestLongEnum.Value2, ByteEnumValue = TestByteEnum.Value3 },
		};

		foreach (var item in testObjects)
			Assert.AreEqual(1, Database.Insert(item));

		var result = Database.Table<TestObj>().ToList();
		Assert.AreEqual(2, testObjects.Length);

		for (var i = 0; i < testObjects.Length; i++)
		{
			var item = testObjects[i];

			Assert.AreEqual(item.Id, result[i].Id);
			Assert.AreEqual(item.EnumValue, result[i].EnumValue);
			Assert.AreEqual(item.LongEnumValue, result[i].LongEnumValue);
			Assert.AreEqual(item.ByteEnumValue, result[i].ByteEnumValue);
		}
	}
}