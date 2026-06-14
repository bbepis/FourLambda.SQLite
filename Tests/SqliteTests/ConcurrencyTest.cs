using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace FourLambda.SQLite.Tests;

[TestFixture, NUnit.Framework.Ignore("These unit tests were disabled in the upstream repo, so I'm assuming they're incomplete")]
[ExcludeFromCodeCoverage]
public class ConcurrencyTest : DBTestHarness
{
	public class TestObj
	{
		[AutoIncrement, PrimaryKey]
		public int Id { get; set; }

		[ExcludeFromCodeCoverage]
		public override string ToString() => $"[TestObj: Id={Id}]";
	}

	private SQLiteConnection CreateMutexConnection(SQLiteOpenFlags? flags = null)
	{
		var connection = new SQLiteConnection(GetDisposablePath(),
			flags ?? SQLiteOpenFlags.FullMutex | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);

		connection.BusyTimeout = TimeSpan.FromSeconds(5);
		connection.CreateTable<TestObj>();

		return connection;
	}

	[Test]
	public void TestLoad()
	{
		//var result = SQLitePCL.raw.sqlite3_threadsafe();
		//Assert.AreEqual(2, result);
		// Yes it's threadsafe on iOS

		var tokenSource = new CancellationTokenSource();

		async Task ReaderTask()
		{
			try
			{
				while (true)
				{
					//
					// NOTE: Change this to readwrite and then it does work ???
					// No more IOERROR
					// 

					using (var dbConnection = CreateMutexConnection(SQLiteOpenFlags.FullMutex | SQLiteOpenFlags.ReadOnly))
					{
						var records = dbConnection.Table<TestObj>().ToList();
						System.Diagnostics.Debug.WriteLine($"{Environment.CurrentManagedThreadId} Read records: {records.Count}");
					}

					// No await so we stay on the same thread
					Task.Delay(10).GetAwaiter().GetResult();
					tokenSource.Token.ThrowIfCancellationRequested();
				}
			}
			catch (OperationCanceledException)
			{
			}
		}

		async Task WriterTask()
		{
			try
			{
				while (true)
				{
					using (var dbConnection = CreateMutexConnection(SQLiteOpenFlags.FullMutex | SQLiteOpenFlags.ReadWrite))
					{
						System.Diagnostics.Debug.WriteLine($"{Environment.CurrentManagedThreadId} Start insert");

						for (var i = 0; i < 50; i++)
						{
							dbConnection.Insert(new TestObj());
						}

						System.Diagnostics.Debug.WriteLine($"{Environment.CurrentManagedThreadId} Inserted records");
					}

					// No await so we stay on the same thread
					Task.Delay(1).GetAwaiter().GetResult();
					tokenSource.Token.ThrowIfCancellationRequested();
				}
			}
			catch (OperationCanceledException)
			{
			}
		}

		var tasks = new List<Task>
		{
			Task.Run(ReaderTask),
			Task.Run(WriterTask)
		};

		// Wait 5sec
		tokenSource.CancelAfter(5000);

		Task.WhenAll(tasks).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Test for issue #761. Because the nature of this test is a race condition,
	/// it is not guaranteed to fail if the issue is present. It does appear to
	/// fail most of the time, though.
	/// </summary>
	[Test]
	public void TestInsertCommandCreation()
	{
		using var mutexConnection = CreateMutexConnection();

		var obj1 = new TestObj();
		var obj2 = new TestObj();
		var taskA = Task.Run(() => { mutexConnection.Insert(obj1); });
		var taskB = Task.Run(() => { mutexConnection.Insert(obj2); });

		Task.WhenAll(taskA, taskB).Wait();
	}
}