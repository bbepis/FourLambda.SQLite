namespace FourLambda.SQLite.Tests;

[TestFixture]
public class IgnoreTest : DBTestHarness
{
	public class TestObj
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }

		public string Text { get; set; }

		protected Dictionary<int, string> _edibles = new();

		[Ignore]
		public Dictionary<int, string> Edibles
		{ 
			get => _edibles;
			set => _edibles = value;
		}

		[Ignore]
		public string IgnoredText { get; set; }

		public override string ToString() => $"[TestObj: Id={Id}]";
	}

	[Test]
	public void MappingIgnoreColumn()
	{
		var m = Database.GetMapping<TestObj>();

		Assert.AreEqual(2, m.Columns.Length);
	}

	[Test]
	public void CreateTableSucceeds()
	{
		Database.CreateTable<TestObj>();
	}

	[Test]
	public void InsertSucceeds()
	{
		Database.CreateTable<TestObj>();

		var o = new TestObj {
			Text = "Hello",
			IgnoredText = "World",
		};

		Database.Insert (o);

		Assert.AreEqual (1, o.Id);
	}

	[Test]
	public void GetDoesNotRetrieveIgnoredProperties ()
	{
		Database.CreateTable<TestObj>();

		var o = new TestObj {
			Text = "Hello",
			IgnoredText = "World",
		};

		Database.Insert (o);

		var oo = Database.Get<TestObj>(o.Id);

		Assert.AreEqual ("Hello", oo.Text);
		Assert.AreEqual (null, oo.IgnoredText);
	}

	public class BaseClass
	{
		[Ignore]
		public string ToIgnore { get; set; }
	}

	public class TableClass : BaseClass
	{
		public string Name { get; set; }
	}

	[Test]
	public void BaseIgnores()
	{
		Database.CreateTable<TableClass>();

		var o = new TableClass {
			ToIgnore = "Hello",
			Name = "World",
		};

		Database.Insert(o);

		var oo = Database.Table<TableClass>().First();

		Assert.AreEqual(null, oo.ToIgnore);
		Assert.AreEqual("World", oo.Name);
	}

	public class RedefinedBaseClass
	{
		public string Name { get; set; }
		public List<string> Values { get; set; }
	}

	public class RedefinedClass : RedefinedBaseClass
	{
		[Ignore]
		public new List<string> Values { get; set; }
		public string Value { get; set; }
	}

	[Test]
	public void RedefinedIgnores ()
	{
		Database.CreateTable<RedefinedClass>();

		var o = new RedefinedClass {
			Name = "Foo",
			Value = "Bar",
			Values = new List<string> { "hello", "world" },
		};

		Database.Insert(o);

		var oo = Database.Table<RedefinedClass>().First();

		Assert.AreEqual ("Foo", oo.Name);
		Assert.AreEqual ("Bar", oo.Value);
		Assert.AreEqual (null, oo.Values);
	}

	[AttributeUsage (AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	class DerivedIgnoreAttribute : IgnoreAttribute
	{
	}

	class DerivedIgnoreClass
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		public string NotIgnored { get; set; }

		[DerivedIgnore]
		public string Ignored { get; set; }
	}

	[Test]
	public void DerivedIgnore ()
	{
		Database.CreateTable<DerivedIgnoreClass>();

		var o = new DerivedIgnoreClass {
			Ignored = "Hello",
			NotIgnored = "World",
		};

		Database.Insert (o);

		var oo = Database.Table<DerivedIgnoreClass>().First ();

		Assert.AreEqual (null, oo.Ignored);
		Assert.AreEqual ("World", oo.NotIgnored);
	}
}