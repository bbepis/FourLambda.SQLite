using System.Diagnostics;

namespace FourLambda.SQLite.Tests;

[TestFixture]
public class InsertTest : DBTestHarness
{
	public class TestObj
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }
		public string Text { get; set; }

		public override string ToString () => $"[TestObj: Id={Id}, Text={Text}]";
	}

	public class TestObj2
	{
		[PrimaryKey]
		public int Id { get; set; }
		public string Text { get; set; }

		public override string ToString() => $"[TestObj: Id={Id}, Text={Text}]";
	}

	public class OneColumnObj
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }
	}

	public class UniqueObj
	{
		[PrimaryKey]
		public int Id { get; set; }
	}

	protected override void InitializeDatabase()
	{
		Database.CreateTable<TestObj>();
		Database.CreateTable<TestObj2>();
		Database.CreateTable<OneColumnObj>();
		Database.CreateTable<UniqueObj>();
	}

	[Test]
	public void InsertALot()
	{
		var n = 10000;
		var q = from i in Enumerable.Range(1, n)
			select new TestObj
			{
				Text = "I am"
			};
		var objs = q.ToArray();
		Database.Trace = false;

		var sw = new Stopwatch();
		sw.Start();

		var numIn = Database.InsertAll(objs);

		sw.Stop();

		Assert.AreEqual(numIn, n, "Num inserted must = num objects");

		var inObjs = Database.CreateCommand("select * from TestObj").ExecuteQuery<TestObj>().ToArray();

		for (var i = 0; i < inObjs.Length; i++)
		{
			Assert.AreEqual(i + 1, objs[i].Id);
			Assert.AreEqual(i + 1, inObjs[i].Id);
			Assert.AreEqual("I am", inObjs[i].Text);
		}

		var numCount = Database.CreateCommand("select count(*) from TestObj").ExecuteScalar<int>();

		Assert.AreEqual(numCount, n, "Num counted must = num objects");
	}

	[Test]
	public void InsertTraces()
	{
		var oldTracer = Database.Tracer;
		var oldTrace = Database.Trace;

		var traces = new List<string>();
		Database.Tracer = traces.Add;
		Database.Trace = true;

		var obj1 = new TestObj { Text = "GLaDOS loves tracing!" };
		var numIn1 = Database.Insert(obj1);

		Assert.AreEqual(1, numIn1);
		Assert.AreEqual(1, traces.Count);

		Database.Tracer = oldTracer;
		Database.Trace = oldTrace;
	}

	[Test]
	public void InsertTwoTimes()
	{
		var obj1 = new TestObj { Text = "GLaDOS loves testing!" };
		var obj2 = new TestObj { Text = "Keep testing, just keep testing" };


		var numIn1 = Database.Insert(obj1);
		var numIn2 = Database.Insert(obj2);
		Assert.AreEqual(1, numIn1);
		Assert.AreEqual(1, numIn2);

		var result = Database.Query<TestObj>("select * from TestObj").ToList();
		Assert.AreEqual(2, result.Count);
		Assert.AreEqual(obj1.Text, result[0].Text);
		Assert.AreEqual(obj2.Text, result[1].Text);
	}

	[Test]
	public void InsertIntoTwoTables()
	{
		var obj1 = new TestObj { Text = "GLaDOS loves testing!" };
		var obj2 = new TestObj2 { Text = "Keep testing, just keep testing" };

		var numIn1 = Database.Insert(obj1);
		Assert.AreEqual(1, numIn1);
		var numIn2 = Database.Insert(obj2);
		Assert.AreEqual(1, numIn2);

		var result1 = Database.Query<TestObj>("select * from TestObj").ToList();
		Assert.AreEqual(numIn1, result1.Count);
		Assert.AreEqual(obj1.Text, result1.First().Text);

		var result2 = Database.Query<TestObj>("select * from TestObj2").ToList();
		Assert.AreEqual(numIn2, result2.Count);
	}

	[Test]
	public void InsertWithExtra()
	{
		var obj1 = new TestObj2 { Id = 1, Text = "GLaDOS loves testing!" };
		var obj2 = new TestObj2 { Id = 1, Text = "Keep testing, just keep testing" };
		var obj3 = new TestObj2 { Id = 1, Text = "Done testing" };

		Database.Insert(obj1);

		Assert.Throws<SQLiteException>(() => Database.Insert(obj2), "Expected unique constraint violation");

		Database.Insert(obj2, "OR REPLACE");


		Assert.Throws<SQLiteException>(() => Database.Insert(obj3), "Expected unique constraint violation");

		Database.Insert(obj3, "OR IGNORE");

		var result = Database.Query<TestObj>("select * from TestObj2").ToList();
		Assert.AreEqual(1, result.Count);
		Assert.AreEqual(obj2.Text, result.First().Text);
	}

	[Test]
	public void InsertIntoOneColumnAutoIncrementTable()
	{
		var obj = new OneColumnObj();
		Database.Insert(obj);

		var result = Database.Get<OneColumnObj>(1);
		Assert.AreEqual(1, result.Id);
	}

	[Test]
	public void InsertAllSuccessOutsideTransaction()
	{
		var testObjects = Enumerable.Range(1, 20).Select(i => new UniqueObj { Id = i }).ToList();

		Database.InsertAll(testObjects);

		Assert.AreEqual(testObjects.Count, Database.Table<UniqueObj>().Count());
	}

	[Test]
	public void InsertAllFailureOutsideTransaction()
	{
		var testObjects = Enumerable.Range(1, 20).Select(i => new UniqueObj { Id = i }).ToList();
		testObjects[testObjects.Count - 1].Id = 1; // causes the insert to fail because of duplicate key

		Assert.Throws<SQLiteException>(() => Database.InsertAll(testObjects));

		Assert.AreEqual(0, Database.Table<UniqueObj>().Count());
	}

	[Test]
	public void InsertAllSuccessInsideTransaction()
	{
		var testObjects = Enumerable.Range(1, 20).Select(i => new UniqueObj { Id = i }).ToList();

		Database.RunInTransaction(() =>
		{
			Database.InsertAll(testObjects);
		});

		Assert.AreEqual(testObjects.Count, Database.Table<UniqueObj>().Count());
	}

	[Test]
	public void InsertAllFailureInsideTransaction()
	{
		var testObjects = Enumerable.Range(1, 20).Select(i => new UniqueObj { Id = i }).ToList();
		testObjects[testObjects.Count - 1].Id = 1; // causes the insert to fail because of duplicate key

		Assert.Throws<SQLiteException>(() =>
		{
			Database.RunInTransaction(() =>
			{
				Database.InsertAll(testObjects);
			});
		});

		Assert.AreEqual(0, Database.Table<UniqueObj>().Count());
	}

	[Test]
	public void InsertOrReplace()
	{
		Database.InsertAll(from i in Enumerable.Range(1, 20) select new TestObj { Text = "#" + i });

		Assert.AreEqual(20, Database.Table<TestObj>().Count());

		var t = new TestObj { Id = 5, Text = "Foo" };
		Database.InsertOrReplace(t);

		var r = (from x in Database.Table<TestObj>() orderby x.Id select x).ToList();
		Assert.AreEqual(20, r.Count);
		Assert.AreEqual("Foo", r[4].Text);
	}
}