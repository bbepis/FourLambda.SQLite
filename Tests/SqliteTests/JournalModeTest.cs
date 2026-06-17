namespace FourLambda.SQLite.Tests;

[TestFixture]
public class JournalModeTest : DBTestHarness
{
	// We need to use disk-based databases here since the default test harness uses an in-memory database, which only supports MEMORY and OFF.

	[TestCase(WriteJournalType.Off)]
	[TestCase(WriteJournalType.Delete)]
	[TestCase(WriteJournalType.Truncate)]
	[TestCase(WriteJournalType.Persist)]
	[TestCase(WriteJournalType.Memory)]
	[TestCase(WriteJournalType.WAL)]
	public void CanSetValidJournalMode(WriteJournalType journalType)
	{
		using var database = new SQLiteConnection(GetDisposablePath());

		database.JournalType = journalType;

		Assert.AreEqual(database.JournalType, journalType);
	}

	[Test]
	public void CannotSetInvalidJournalMode()
	{
		using var database = new SQLiteConnection(GetDisposablePath());

		database.JournalType = WriteJournalType.Delete;

		Assert.Throws<ArgumentOutOfRangeException>(() =>
		{
			database.JournalType = (WriteJournalType)10;
		});

		Assert.AreEqual(database.JournalType, WriteJournalType.Delete);
	}
}