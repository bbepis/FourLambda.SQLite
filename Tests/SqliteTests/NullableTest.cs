namespace FourLambda.SQLite.Tests;

[TestFixture]
public class NullableTest : DBTestHarness
{
	public class NullableIntClass
	{
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		public int? NullableInt { get; set; }

		public override bool Equals(object obj)
		{
			var other = (NullableIntClass)obj;
			return ID == other.ID && NullableInt == other.NullableInt;
		}

		public override int GetHashCode () => HashCode.Combine(ID, NullableInt);
	}

	[Test, Description("Create a table with a nullable int column then insert and select against it")]
	public void NullableInt()
	{
		Database.CreateTable<NullableIntClass>();

		var withNull = new NullableIntClass { NullableInt = null };
		var with0 = new NullableIntClass { NullableInt = 0 };
		var with1 = new NullableIntClass { NullableInt = 1 };
		var withMinus1 = new NullableIntClass { NullableInt = -1 };

		Database.Insert(withNull);
		Database.Insert(with0);
		Database.Insert(with1);
		Database.Insert(withMinus1);

		var results = Database.Table<NullableIntClass>().OrderBy(x => x.ID).ToArray();

		Assert.AreEqual(4, results.Length);

		Assert.AreEqual(withNull, results[0]);
		Assert.AreEqual(with0, results[1]);
		Assert.AreEqual(with1, results[2]);
		Assert.AreEqual(withMinus1, results[3]);
	}


	public class NullableFloatClass
	{
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		public float? NullableFloat { get; set; }

		public override bool Equals(object obj)
		{
			var other = (NullableFloatClass)obj;
			return ID == other.ID && NullableFloat == other.NullableFloat;
		}

		public override int GetHashCode() => HashCode.Combine(ID, NullableFloat);
	}

	[Test, Description("Create a table with a nullable int column then insert and select against it")]
	public void NullableFloat()
	{
		Database.CreateTable<NullableFloatClass>();

		var withNull = new NullableFloatClass { NullableFloat = null };
		var with0 = new NullableFloatClass { NullableFloat = 0 };
		var with1 = new NullableFloatClass { NullableFloat = 1 };
		var withMinus1 = new NullableFloatClass { NullableFloat = -1 };

		Database.Insert(withNull);
		Database.Insert(with0);
		Database.Insert(with1);
		Database.Insert(withMinus1);

		var results = Database.Table<NullableFloatClass>().OrderBy(x => x.ID).ToArray();

		Assert.AreEqual(4, results.Length);

		Assert.AreEqual(withNull, results[0]);
		Assert.AreEqual(with0, results[1]);
		Assert.AreEqual(with1, results[2]);
		Assert.AreEqual(withMinus1, results[3]);
	}


	public class StringClass
	{
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		//Strings are allowed to be null by default
		public string StringData { get; set; }

		public override bool Equals(object obj)
		{
			var other = (StringClass)obj;
			return ID == other.ID && StringData == other.StringData;
		}

		public override int GetHashCode() => HashCode.Combine(ID, StringData);
	}

	[Test]
	public void NullableString()
	{
		Database.CreateTable<StringClass>();

		var withNull = new StringClass { StringData = null };
		var withEmpty = new StringClass { StringData = "" };
		var withData = new StringClass { StringData = "data" };

		Database.Insert(withNull);
		Database.Insert(withEmpty);
		Database.Insert(withData);

		var results = Database.Table<StringClass>().OrderBy(x => x.ID).ToArray();

		Assert.AreEqual(3, results.Length);

		Assert.AreEqual(withNull, results[0]);
		Assert.AreEqual(withEmpty, results[1]);
		Assert.AreEqual(withData, results[2]);
	}

	[Test]
	public void WhereNotNull()
	{
		Database.CreateTable<NullableIntClass>();

		var withNull = new NullableIntClass { NullableInt = null };
		var with0 = new NullableIntClass { NullableInt = 0 };
		var with1 = new NullableIntClass { NullableInt = 1 };
		var withMinus1 = new NullableIntClass { NullableInt = -1 };

		Database.Insert(withNull);
		Database.Insert(with0);
		Database.Insert(with1);
		Database.Insert(withMinus1);

		var results = Database.Table<NullableIntClass>().Where(x => x.NullableInt != null).OrderBy(x => x.ID).ToArray();

		Assert.AreEqual(3, results.Length);

		Assert.AreEqual(with0, results[0]);
		Assert.AreEqual(with1, results[1]);
		Assert.AreEqual(withMinus1, results[2]);
	}

	[Test]
	public void WhereNull()
	{
		Database.CreateTable<NullableIntClass>();

		var withNull = new NullableIntClass { NullableInt = null };
		var with0 = new NullableIntClass { NullableInt = 0 };
		var with1 = new NullableIntClass { NullableInt = 1 };
		var withMinus1 = new NullableIntClass { NullableInt = -1 };

		Database.Insert(withNull);
		Database.Insert(with0);
		Database.Insert(with1);
		Database.Insert(withMinus1);

		var results = Database.Table<NullableIntClass>().Where(x => x.NullableInt == null).OrderBy(x => x.ID).ToArray();

		Assert.AreEqual(1, results.Length);
		Assert.AreEqual(withNull, results[0]);
	}

	[Test]
	public void StringWhereNull()
	{
		Database.CreateTable<StringClass>();

		var withNull = new StringClass { StringData = null };
		var withEmpty = new StringClass { StringData = "" };
		var withData = new StringClass { StringData = "data" };

		Database.Insert(withNull);
		Database.Insert(withEmpty);
		Database.Insert(withData);

		var results = Database.Table<StringClass>().Where(x => x.StringData == null).OrderBy(x => x.ID).ToArray();
		Assert.AreEqual(1, results.Length);
		Assert.AreEqual(withNull, results[0]);
	}

	[Test]
	public void StringWhereNotNull()
	{
		Database.CreateTable<StringClass>();

		var withNull = new StringClass { StringData = null };
		var withEmpty = new StringClass { StringData = "" };
		var withData = new StringClass { StringData = "data" };

		Database.Insert(withNull);
		Database.Insert(withEmpty);
		Database.Insert(withData);

		var results = Database.Table<StringClass>().Where(x => x.StringData != null).OrderBy(x => x.ID).ToArray();
		Assert.AreEqual(2, results.Length);
		Assert.AreEqual(withEmpty, results[0]);
		Assert.AreEqual(withData, results[1]);
	}

	public enum TestIntEnum
	{
		One = 1,
		Two = 2
	}

	[StoreAsText]
	public enum TestTextEnum
	{
		Alpha,
		Beta
	}

	public class NullableEnumClass
	{
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		public TestIntEnum? NullableIntEnum { get; set; }
		public TestTextEnum? NullableTextEnum { get; set; }

		public override bool Equals(object obj)
		{
			var other = (NullableEnumClass)obj;
			return ID == other.ID && NullableIntEnum == other.NullableIntEnum &&
			       NullableTextEnum == other.NullableTextEnum;
		}

		public override int GetHashCode() => HashCode.Combine(ID, NullableIntEnum, NullableTextEnum);

		public override string ToString()
			=> $"[NullableEnumClass: ID={ID}, NullableIntEnum={NullableIntEnum}, NullableTextEnum={NullableTextEnum}]";
	}

	[Test, Description("Create a table with a nullable enum column then insert and select against it")]
	public void NullableEnum()
	{
		Database.CreateTable<NullableEnumClass>();

		var withNull = new NullableEnumClass { NullableIntEnum = null, NullableTextEnum = null };
		var with1 = new NullableEnumClass { NullableIntEnum = TestIntEnum.One, NullableTextEnum = null };
		var with2 = new NullableEnumClass { NullableIntEnum = TestIntEnum.Two, NullableTextEnum = null };
		var withNullA = new NullableEnumClass { NullableIntEnum = null, NullableTextEnum = TestTextEnum.Alpha };
		var with1B = new NullableEnumClass { NullableIntEnum = TestIntEnum.One, NullableTextEnum = TestTextEnum.Beta };

		Database.Insert(withNull);
		Database.Insert(with1);
		Database.Insert(with2);
		Database.Insert(withNullA);
		Database.Insert(with1B);

		var results = Database.Table<NullableEnumClass>().OrderBy(x => x.ID).ToArray();

		Assert.AreEqual(5, results.Length);

		Assert.AreEqual(withNull, results[0]);
		Assert.AreEqual(with1, results[1]);
		Assert.AreEqual(with2, results[2]);
		Assert.AreEqual(withNullA, results[3]);
		Assert.AreEqual(with1B, results[4]);
	}
}