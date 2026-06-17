namespace FourLambda.SQLite.Tests;

[TestFixture]
public class AsyncExtensionsTest : DBTestHarness
{
    private class TestObj
    {
        [AutoIncrement, PrimaryKey]
        public int Id { get; set; }
        public string Text { get; set; }
        public int Value { get; set; }
    }

    protected override void InitializeDatabase()
    {
        Database.CreateTable<TestObj>();
        Database.CreateTable<Product>();
    }

    [Test]
    public async Task ExecuteAsync_InsertsRow()
    {
        var rows = await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('hello', 42)");
        Assert.AreEqual(1, rows);
        Assert.AreEqual(1, Database.Table<TestObj>().Count());
    }

    [Test]
    public async Task ExecuteAsync_WithParameters()
    {
        var rows = await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES (?, ?)", "param-test", 99);
        Assert.AreEqual(1, rows);
        var item = Database.Find<TestObj>(1);
        Assert.IsNotNull(item);
    }

    [Test]
    public async Task ExecuteScalarAsync_ReturnsCount()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('a', 1)");
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('b', 2)");

        var count = await Database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TestObj");
        Assert.AreEqual(2, count);
    }

    [Test]
    public async Task ExecuteScalarAsync_ReturnsString()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('scalar-test', 10)");

        var text = await Database.ExecuteScalarAsync<string>("SELECT Text FROM TestObj WHERE Id = 1");
        Assert.AreEqual("scalar-test", text);
    }

    [Test]
    public async Task QueryAsync_ReturnsAll()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('q1', 10)");
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('q2', 20)");

        var results = Database.QueryAsync<TestObj>();
        Assert.AreEqual(2, await results.CountAsync());
    }

    [Test]
    public async Task QueryAsync_WithSqlAndArgs()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('low', 5)");
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('high', 100)");

        var results = Database.QueryAsync<TestObj>("SELECT * FROM TestObj WHERE Value > ?", 10);
        Assert.AreEqual(1, await results.CountAsync());
    }

    [Test]
    public async Task QueryAsync_WithPredicate()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('x', 50)");
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('y', 200)");

        var results = Database.QueryAsync<TestObj>(p => p.Value > 100);
        Assert.AreEqual(1, await results.CountAsync());
    }

    [Test]
    public async Task QueryAsync_WithTableMapping()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('map', 30)");

        var map = Database.GetMapping<TestObj>();
        var results = Database.QueryAsync<object>(map, "SELECT * FROM TestObj WHERE Text = ?", "map");
        Assert.AreEqual(1, await results.CountAsync());
    }

    [Test]
    public async Task FindAsync_ByPrimaryKey()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('find-me', 500)");

        var item = await Database.FindAsync<TestObj>(1);
        Assert.IsNotNull(item);
        Assert.AreEqual("find-me", item.Text);
    }

    [Test]
    public async Task FindAsync_ByPrimaryKey_ReturnsNull()
    {
        var item = await Database.FindAsync<TestObj>(9999);
        Assert.IsNull(item);
    }

    [Test]
    public async Task FindAsync_ByPredicate()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('pred', 777)");
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('other', 888)");

        var item = await Database.FindAsync<TestObj>(p => p.Value == 777);
        Assert.IsNotNull(item);
        Assert.AreEqual("pred", item.Text);
    }

    [Test]
    public async Task FindAsync_ByTableMapping()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('tm', 123)");

        var map = Database.GetMapping<TestObj>();
        var item = await Database.FindAsync<object>(map, 1);
        Assert.IsNotNull(item);
    }

    [Test]
    public async Task InsertAsync_SingleObject()
    {
        var rows = await Database.InsertAsync(new TestObj { Text = "async-insert", Value = 1 });
        Assert.AreEqual(1, rows);

        var item = Database.Find<TestObj>(1);
        Assert.IsNotNull(item);
        Assert.AreEqual("async-insert", item.Text);
    }

    [Test]
    public async Task InsertAsync_WithConflictAction()
    {
        await Database.InsertAsync(new TestObj { Text = "first", Value = 1 });
        var rows = await Database.InsertAsync(new TestObj { Text = "second", Value = 2 }, InsertConflictAction.Abort);
        Assert.AreEqual(1, rows);
        Assert.AreEqual(2, Database.Table<TestObj>().Count());
    }

    [Test]
    public async Task InsertAllAsync_MultipleObjects()
    {
        var items = Enumerable.Range(0, 10)
            .Select(i => new TestObj { Text = "batch-" + i, Value = i })
            .ToList();

        var rows = await Database.InsertAllAsync(items);
        Assert.AreEqual(10, rows);
        Assert.AreEqual(10, Database.Table<TestObj>().Count());
    }

    [Test]
    public async Task InsertAllAsync_OutsideTransaction()
    {
        var items = Enumerable.Range(0, 5)
            .Select(i => new TestObj { Text = "notx-" + i, Value = i })
            .ToList();

        var rows = await Database.InsertAllAsync(items, runInTransaction: false);
        Assert.AreEqual(5, rows);
    }

    [Test]
    public async Task InsertAsync_WithTableMapping()
    {
        var map = Database.GetMapping<TestObj>();
        var obj = new TestObj { Text = "map-insert", Value = 99 };
        var rows = await Database.InsertAsync(map, obj);
        Assert.AreEqual(1, rows);

        var item = Database.Find<TestObj>(obj.Id);
        Assert.IsNotNull(item);
    }

    [Test]
    public async Task InsertAllAsync_WithTableMapping()
    {
        var map = Database.GetMapping<TestObj>();
        var items = Enumerable.Range(0, 3)
            .Select(i => new TestObj { Text = "mapb-" + i, Value = i })
            .ToList();

        var rows = await Database.InsertAllAsync(map, items);
        Assert.AreEqual(3, rows);
    }

    [Test]
    public async Task UpdateAsync_SingleObject()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('before', 10)");

        var item = Database.Find<TestObj>(1);
        item.Text = "after";
        item.Value = 20;

        var rows = await Database.UpdateAsync(item);
        Assert.AreEqual(1, rows);

        var updated = Database.Find<TestObj>(1);
        Assert.AreEqual("after", updated.Text);
        Assert.AreEqual(20, updated.Value);
    }

    [Test]
    public async Task UpdateAllAsync_MultipleObjects()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('u1', 1)");
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('u2', 2)");

        var items = Database.Query<TestObj>().ToList();
        foreach (var item in items)
            item.Text = "updated";

        var rows = await Database.UpdateAllAsync(items);
        Assert.AreEqual(2, rows);

        var updated = Database.Query<TestObj>().ToList();
        Assert.True(updated.All(p => p.Text == "updated"));
    }

    [Test]
    public async Task UpdateAllAsync_OutsideTransaction()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('o1', 100)");
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('o2', 200)");

        var items = Database.Query<TestObj>().ToList();
        foreach (var item in items)
            item.Value += 1;

        var rows = await Database.UpdateAllAsync(items, runInTransaction: false);
        Assert.AreEqual(2, rows);
    }

    [Test]
    public async Task UpdateAsync_WithTableMapping()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('mu', 50)");

        var item = Database.Find<TestObj>(1);
        item.Text = "mapped-update";

        var map = Database.GetMapping<TestObj>();
        var rows = await Database.UpdateAsync(map, item);
        Assert.AreEqual(1, rows);
    }

    [Test]
    public async Task UpdateAllAsync_WithTableMapping()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('ma1', 5)");
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('ma2', 6)");

        var map = Database.GetMapping<TestObj>();
        var items = Database.Query<TestObj>().ToList();
        foreach (var item in items)
            item.Text = "mapped-batch";

        var rows = await Database.UpdateAllAsync(map, items);
        Assert.AreEqual(2, rows);
    }

    [Test]
    public async Task DeleteAsync_ByEntity()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('del', 1)");

        var item = Database.Find<TestObj>(1);
        var rows = await Database.DeleteAsync(item);
        Assert.AreEqual(1, rows);
        Assert.AreEqual(0, Database.Table<TestObj>().Count());
    }

    [Test]
    public async Task DeleteAsync_ByPrimaryKey()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('dpk', 2)");

        var rows = await Database.DeleteAsync<TestObj>(1);
        Assert.AreEqual(1, rows);
        Assert.AreEqual(0, Database.Table<TestObj>().Count());
    }

    [Test]
    public async Task DeleteAsync_NonExistent()
    {
        var rows = await Database.DeleteAsync<TestObj>(9999);
        Assert.AreEqual(0, rows);
    }

    [Test]
    public async Task DeleteAllAsync_ByType()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('da1', 1)");
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('da2', 2)");

        var rows = await Database.DeleteAllAsync<TestObj>();
        Assert.AreEqual(2, rows);
        Assert.AreEqual(0, Database.Table<TestObj>().Count());
    }

    [Test]
    public async Task DeleteAsync_WithTableMapping()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('dtm', 3)");

        var map = Database.GetMapping<TestObj>();
        var rows = await Database.DeleteAsync(map, 1);
        Assert.AreEqual(1, rows);
    }

    [Test]
    public async Task DeleteAllAsync_WithTableMapping()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('dta1', 10)");
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('dta2', 20)");

        var map = Database.GetMapping<TestObj>();
        var rows = await Database.DeleteAllAsync(map);
        Assert.AreEqual(2, rows);
    }

    private class AsyncCreateTableObj
    {
        [AutoIncrement, PrimaryKey]
        public int Id { get; set; }
        public string Text { get; set; }
    }

    [Test]
    public async Task CreateTableAsync_CreatesTable()
    {
        var result = await Database.CreateTableAsync<AsyncCreateTableObj>();
        Assert.AreEqual(SQLiteConnection.CreateTableResult.Created, result);
    }

    [Test]
    public async Task CreateTableAsync_ExistingTable()
    {
        var result1 = await Database.CreateTableAsync<AsyncCreateTableObj>();
        Assert.AreEqual(SQLiteConnection.CreateTableResult.Created, result1);

        var result2 = await Database.CreateTableAsync<AsyncCreateTableObj>();
        Assert.AreEqual(SQLiteConnection.CreateTableResult.Migrated, result2);
    }

    [Test]
    public async Task DropTableAsync_DropsTable()
    {
        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('drop', 1)");
        var rows = await Database.DropTableAsync<TestObj>();
        Assert.AreEqual(1, rows);

        Assert.Throws<SQLiteException>(() => Database.Query<TestObj>().ToList());
    }

    [Test]
    public async Task CreateIndexAsync_CreatesIndex()
    {
        var rows = await Database.CreateIndexAsync("Product", "Name");
        Assert.AreEqual(0, rows);
    }

    [Test]
    public async Task GetTableInfoAsync_ReturnsColumns()
    {
        var cols = await Database.GetTableInfoAsync("TestObj");
        Assert.AreEqual(3, cols.Length);
        Assert.True(cols.Any(c => c.Name == "Id"));
        Assert.True(cols.Any(c => c.Name == "Text"));
        Assert.True(cols.Any(c => c.Name == "Value"));
    }

    [Test]
    public async Task BackupAsync_BacksUp()
    {
        var pathDest = GetDisposablePath();

        await Database.ExecuteAsync("INSERT INTO TestObj (Text, Value) VALUES ('backup', 1)");

        await Database.BackupAsync(pathDest, "main");

        var destLen = new FileInfo(pathDest).Length;
        Assert.True(destLen >= 4096);
    }
}
