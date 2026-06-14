using System.Diagnostics.CodeAnalysis;

namespace FourLambda.SQLite.Tests;

[TestFixture]
public class TransactionTest : DBTestHarness
{
	private List<TestObj> testObjects;

	public class TestObj
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }


		[ExcludeFromCodeCoverage]
		public override string ToString() => $"[TestObj: Id={Id}]";
	}

	public class TransactionTestException : Exception
	{
	}

	protected override void InitializeDatabase()
	{
		Database.CreateTable<TestObj>();

		testObjects = Enumerable.Range(1, 20).Select(i => new TestObj()).ToList();
		Database.InsertAll(testObjects);
	}

	[Test]
	public void SuccessfulSavepointTransaction()
	{
		using (var scope = Database.CreateTransactionScope())
		{
			Database.Delete(testObjects[0]);
			Database.Delete(testObjects[1]);
			Database.Insert(new TestObj());

			scope.Commit();
		}

		Assert.AreEqual(testObjects.Count - 1, Database.Table<TestObj>().Count());
	}

	[Test]
	public void FailSavepointTransaction()
	{
		try
		{
			using (var scope = Database.CreateTransactionScope())
			{
				Database.Delete(testObjects[0]);

				throw new TransactionTestException();
				scope.Commit();
			}
		}
		catch (TransactionTestException)
		{
			// ignore
		}

		Assert.AreEqual(testObjects.Count, Database.Table<TestObj>().Count());
	}

	[Test]
	public void SuccessfulNestedSavepointTransaction()
	{
		using (var scope = Database.CreateTransactionScope())
		{
			Database.Delete(testObjects[0]);

			using (var scope2 = Database.CreateTransactionScope())
			{
				Database.Delete(testObjects[1]);
				scope2.Commit();
			}

			scope.Commit();
		}

		Assert.AreEqual(testObjects.Count - 2, Database.Table<TestObj>().Count());
	}

	[Test]
	public void FailNestedSavepointTransaction()
	{
		try
		{
			using (var scope = Database.CreateTransactionScope())
			{
				Database.Delete(testObjects[0]);

				using (var scope2 = Database.CreateTransactionScope())
				{
					Database.Delete(testObjects[1]);

					throw new TransactionTestException();
					scope2.Commit();
				}

				scope.Commit();
			}
		}
		catch (TransactionTestException)
		{
			// ignore
		}

		Assert.AreEqual(testObjects.Count, Database.Table<TestObj>().Count());
	}

	[Test]
	public void Issue604_RecoversFromFailedCommit()
	{
		var initialCount = Database.Table<TestObj>().Count();

		//
		// Well this is an issue because there is an internal variable called _transactionDepth
		// that tries to track if we are in an active transaction.
		// The problem is, _transactionDepth is set to 0 and then commit is executed on the database.
		// Well, the commit fails and "When COMMIT fails in this way, the transaction remains active and
		// the COMMIT can be retried later after the reader has had a chance to clear"
		//
		var rollbacks = 0;
		Database.Tracer = m =>
		{
			Console.WriteLine(m);

			if (m == "Executing: commit")
				throw new SQLiteException(SQLite3Native.Result.Busy, "Make commit fail");
			if (m == "Executing: rollback")
				rollbacks++;
		};
		Database.BeginTransaction();
		Database.Insert(new TestObj());

		var ex = Assert.Throws<SQLiteException>(Database.Commit);
		Assert.AreEqual(SQLite3Native.Result.Busy, ex.Result);

		Assert.False(Database.IsInTransaction);
		Assert.AreEqual(1, rollbacks);

		//
		// The catch statements in the RunInTransaction family of functions catch this and call rollback,
		// but since _transactionDepth is 0, the transaction isn't actually rolled back.
		//
		// So the next time begin transaction is called on the same connection,
		// sqlite-net attempts to begin a new transaction (because _transactionDepth is 0),
		// which promptly fails because there is still an active transaction on the connection.
		//
		// Well now we are in big trouble because _transactionDepth got set to 1,
		// and when begin transaction fails in this manner, the transaction isn't rolled back
		// (which would have set _transactionDepth to 0)
		//
		Database.Tracer = Console.WriteLine;
		Database.BeginTransaction();
		Database.Insert(new TestObj());
		Database.Commit();
		Assert.AreEqual(initialCount + 1, Database.Table<TestObj>().Count());
	}

	[Test]
	public void Issue604_RecoversFromFailedRelease()
	{
		var initialCount = Database.Table<TestObj>().Count();

		var rollbacks = 0;
		Database.Tracer = m =>
		{
			//Console.WriteLine (m);
			if (m.StartsWith("Executing: release"))
				throw new SQLiteException(SQLite3Native.Result.Busy, "Make release fail");
			if (m == "Executing: rollback")
				rollbacks++;
		};
		var sp0 = Database.SaveTransactionPoint();
		Database.Insert(new TestObj());

		var ex = Assert.Throws<SQLiteException>(() => Database.Release(sp0));
		Assert.AreEqual(SQLite3Native.Result.Busy, ex.Result);

		Assert.False(Database.IsInTransaction);
		Assert.AreEqual(1, rollbacks);

		Database.BeginTransaction();
		Database.Insert(new TestObj());
		Database.Commit();
		Assert.AreEqual(initialCount + 1, Database.Table<TestObj>().Count());
	}
}