namespace FourLambda.SQLite.Tests;

public abstract class DBTestHarness
{
	protected SQLiteConnection Database { get; private set; }
	private List<string> temporaryPaths = new();

	protected virtual void InitializeDatabase() { }

	protected string GetDisposablePath(string suffix = "")
	{
		var tempPath = Path.GetTempFileName() + suffix + ".db";
		temporaryPaths.Add(tempPath);
		return tempPath;
	}

	[SetUp]
	protected void Setup()
	{
		Database = new SQLiteConnection(":memory:");
		Database.Trace = true;
		Database.Tracer = Console.WriteLine;
		InitializeDatabase();
	}

	[TearDown]
	protected void Teardown()
	{
		Database.Close();

		foreach (var path in temporaryPaths.ToArray())
		{
			try
			{
				if (File.Exists(path))
					File.Delete(path);
			}
			finally
			{
				temporaryPaths.Remove(path);
			}
		}
	}
}