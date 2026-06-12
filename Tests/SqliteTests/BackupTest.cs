namespace FourLambda.SQLite.Tests;

[TestFixture]
public class BackupTest : DBTestHarness
{
	[Test]
	public void BackupOneTable()
	{
		var pathDest = GetDisposablePath();

		Database.CreateTable<OrderLine>();
		Database.Insert(new OrderLine { });
		var lines = Database.Table<OrderLine>().ToList();
		Assert.AreEqual(1, lines.Count);

		Database.Backup(pathDest);

		var destLen = new FileInfo(pathDest).Length;
		Assert.True(destLen >= 4096);
	}
}