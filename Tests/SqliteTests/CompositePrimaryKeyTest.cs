namespace FourLambda.SQLite.Tests;

[TestFixture]
public class CompositePrimaryKeyTest : DBTestHarness
{
    class StudentCourse
    {
        [PrimaryKey(1)]
        public int StudentId { get; set; }

        [PrimaryKey(2)]
        public int CourseId { get; set; }

        public decimal Grade { get; set; }
    }

    class UserRole
    {
        [PrimaryKey(1)]
        public string UserId { get; set; }

        [PrimaryKey(2)]
        public string RoleName { get; set; }

        public DateTime AssignedAt { get; set; }
    }

    class MatrixCell
    {
        [PrimaryKey(1)]
        public int Row { get; set; }

        [PrimaryKey(2)]
        public int Col { get; set; }

        [PrimaryKey(3)]
        public int Layer { get; set; }

        public double Value { get; set; }
    }

    class BadAutoIncComposite
    {
        [PrimaryKey(1), AutoIncrement]
        public int Id1 { get; set; }

        [PrimaryKey(2)]
        public int Id2 { get; set; }

        public string Data { get; set; }
    }

    class BadDuplicateOrderComposite
    {
        [PrimaryKey]
        public int A { get; set; }

        [PrimaryKey]
        public int B { get; set; }

        public string Data { get; set; }
    }

    [Test]
    public void CreateCompositePKTable()
    {
        var result = Database.CreateTable<StudentCourse>();
        Assert.AreEqual(SQLiteConnection.CreateTableResult.Created, result);

        var map = Database.GetMapping<StudentCourse>();
        Assert.AreEqual(2, map.PrimaryKeyColumns.Length);
        Assert.AreEqual("StudentId", map.PrimaryKeyColumns[0].Name);
        Assert.AreEqual("CourseId", map.PrimaryKeyColumns[1].Name);
        Assert.That(map.HasAutoIncPK, Is.False);
    }

    [Test]
    public void CreateCompositePKTableWithStrings()
    {
        var result = Database.CreateTable<UserRole>();
        Assert.AreEqual(SQLiteConnection.CreateTableResult.Created, result);

        var map = Database.GetMapping<UserRole>();
        Assert.AreEqual(2, map.PrimaryKeyColumns.Length);
        Assert.AreEqual("UserId", map.PrimaryKeyColumns[0].Name);
        Assert.AreEqual("RoleName", map.PrimaryKeyColumns[1].Name);
    }

    [Test]
    public void CreateThreeColumnCompositePK()
    {
        var result = Database.CreateTable<MatrixCell>();
        Assert.AreEqual(SQLiteConnection.CreateTableResult.Created, result);

        var map = Database.GetMapping<MatrixCell>();
        Assert.AreEqual(3, map.PrimaryKeyColumns.Length);
        Assert.AreEqual("Row", map.PrimaryKeyColumns[0].Name);
        Assert.AreEqual("Col", map.PrimaryKeyColumns[1].Name);
        Assert.AreEqual("Layer", map.PrimaryKeyColumns[2].Name);
    }

    [Test]
    public void RejectAutoIncrementInCompositePK()
    {
        Assert.Throws<ArgumentException>(() => Database.CreateTable<BadAutoIncComposite>());
    }

    [Test]
    public void RejectDuplicateOrderInCompositePK()
    {
        Assert.Throws<ArgumentException>(() => Database.CreateTable<BadDuplicateOrderComposite>());
    }

    [Test]
    public void InsertIntoCompositePKTable()
    {
        Database.CreateTable<StudentCourse>();

        var row1 = new StudentCourse { StudentId = 1, CourseId = 101, Grade = 3.5m };
        var row2 = new StudentCourse { StudentId = 1, CourseId = 102, Grade = 4.0m };
        var row3 = new StudentCourse { StudentId = 2, CourseId = 101, Grade = 3.8m };

        Assert.AreEqual(1, Database.Insert(row1));
        Assert.AreEqual(1, Database.Insert(row2));
        Assert.AreEqual(1, Database.Insert(row3));

        Assert.AreEqual(3, Database.Table<StudentCourse>().Count());
    }

    [Test]
    public void InsertDuplicateCompositePKThrows()
    {
        Database.CreateTable<StudentCourse>();

        var row1 = new StudentCourse { StudentId = 1, CourseId = 101, Grade = 3.5m };
        var row2 = new StudentCourse { StudentId = 1, CourseId = 101, Grade = 4.0m };

        Database.Insert(row1);
        Assert.Throws<SQLiteException>(() => Database.Insert(row2));
    }

    [Test]
    public void InsertAllCompositePK()
    {
        Database.CreateTable<StudentCourse>();

        var rows = new[]
        {
            new StudentCourse { StudentId = 1, CourseId = 101, Grade = 3.5m },
            new StudentCourse { StudentId = 1, CourseId = 102, Grade = 4.0m },
            new StudentCourse { StudentId = 2, CourseId = 101, Grade = 3.8m },
            new StudentCourse { StudentId = 2, CourseId = 102, Grade = 3.2m },
        };

        var count = Database.InsertAll(rows);
        Assert.AreEqual(4, count);
        Assert.AreEqual(4, Database.Table<StudentCourse>().Count());
    }

    [Test]
    public void FindByCompositePK()
    {
        Database.CreateTable<StudentCourse>();

        var sourceObjects = new[]
        {
	        new StudentCourse { StudentId = 1, CourseId = 101, Grade = 3.5m },
	        new StudentCourse { StudentId = 1, CourseId = 102, Grade = 4.0m },
	        new StudentCourse { StudentId = 2, CourseId = 101, Grade = 3.8m }
        };

        foreach (var value in sourceObjects)
			Database.Insert(value);

        foreach (var value in sourceObjects)
        {
	        var found = Database.Find<StudentCourse>(value.StudentId, value.CourseId);
	        Assert.IsNotNull(found);
	        Assert.AreEqual(value.StudentId, found.StudentId);
	        Assert.AreEqual(value.CourseId, found.CourseId);
	        Assert.AreEqual(value.Grade, found.Grade);
        }

        var notFound = Database.Find<StudentCourse>(3, 999);
        Assert.That(notFound, Is.Null);
    }

    [Test]
    public void FindByCompositePKWithStrings()
    {
        Database.CreateTable<UserRole>();

        Database.Insert(new UserRole { UserId = "user1", RoleName = "admin", AssignedAt = new DateTime(2024, 1, 1) });
        Database.Insert(new UserRole { UserId = "user1", RoleName = "editor", AssignedAt = new DateTime(2024, 6, 1) });

        var found = Database.Find<UserRole>("user1", "admin");
        Assert.That(found, Is.Not.Null);
        Assert.AreEqual("user1", found.UserId);
        Assert.AreEqual("admin", found.RoleName);

        var notFound = Database.Find<UserRole>("user2", "admin");
        Assert.That(notFound, Is.Null);
    }

    [Test]
    public void FindByThreeColumnCompositePK()
    {
        Database.CreateTable<MatrixCell>();

        Database.Insert(new MatrixCell { Row = 1, Col = 1, Layer = 0, Value = 1.5 });
        Database.Insert(new MatrixCell { Row = 1, Col = 1, Layer = 1, Value = 2.5 });

        var found = Database.Find<MatrixCell>(1, 1, 0);
        Assert.That(found, Is.Not.Null);
        Assert.AreEqual(1.5, found.Value);

        var found2 = Database.Find<MatrixCell>(1, 1, 1);
        Assert.That(found2, Is.Not.Null);
        Assert.AreEqual(2.5, found2.Value);
    }

    [Test]
    public void UpdateCompositePKRow()
    {
        Database.CreateTable<StudentCourse>();

        var row = new StudentCourse { StudentId = 1, CourseId = 101, Grade = 3.5m };
        Database.Insert(row);

        row.Grade = 4.0m;
        var affected = Database.Update(row);
        Assert.AreEqual(1, affected);

        var updated = Database.Find<StudentCourse>(1, 101);
        Assert.That(updated, Is.Not.Null);
        Assert.AreEqual(4.0m, updated.Grade);
    }

    [Test]
    public void UpdateAllCompositePKRows()
    {
        Database.CreateTable<StudentCourse>();

        var rows = new[]
        {
            new StudentCourse { StudentId = 1, CourseId = 101, Grade = 3.5m },
            new StudentCourse { StudentId = 1, CourseId = 102, Grade = 4.0m },
        };

        Database.InsertAll(rows);

        foreach (var r in rows)
            r.Grade += 0.5m;

        var affected = Database.UpdateAll(rows);
        Assert.AreEqual(2, affected);

        var updated1 = Database.Find<StudentCourse>(1, 101);
        var updated2 = Database.Find<StudentCourse>(1, 102);
        Assert.AreEqual(4.0m, updated1.Grade);
        Assert.AreEqual(4.5m, updated2.Grade);
    }

    [Test]
    public void DeleteCompositePKRowByObject()
    {
        Database.CreateTable<StudentCourse>();

        Database.Insert(new StudentCourse { StudentId = 1, CourseId = 101, Grade = 3.5m });
        Database.Insert(new StudentCourse { StudentId = 1, CourseId = 102, Grade = 4.0m });

        var affected = Database.Delete(new StudentCourse { StudentId = 1, CourseId = 101 });
        Assert.AreEqual(1, affected);

        Assert.AreEqual(1, Database.Table<StudentCourse>().Count());
        Assert.That(Database.Find<StudentCourse>(1, 101), Is.Null);
        Assert.That(Database.Find<StudentCourse>(1, 102), Is.Not.Null);
    }

    [Test]
    public void DeleteCompositePKRowByKeys()
    {
        Database.CreateTable<StudentCourse>();

        Database.Insert(new StudentCourse { StudentId = 1, CourseId = 101, Grade = 3.5m });
        Database.Insert(new StudentCourse { StudentId = 2, CourseId = 101, Grade = 3.8m });

        var affected = Database.Delete<StudentCourse>(2, 101);
        Assert.AreEqual(1, affected);

        Assert.AreEqual(1, Database.Table<StudentCourse>().Count());
        Assert.That(Database.Find<StudentCourse>(1, 101), Is.Not.Null);
        Assert.That(Database.Find<StudentCourse>(2, 101), Is.Null);
    }

    [Test]
    public void DeleteAllCompositePKRows()
    {
        Database.CreateTable<StudentCourse>();

        Database.Insert(new StudentCourse { StudentId = 1, CourseId = 101, Grade = 3.5m });
        Database.Insert(new StudentCourse { StudentId = 2, CourseId = 102, Grade = 4.0m });

        var affected = Database.DeleteAll<StudentCourse>();
        Assert.AreEqual(2, affected);
        Assert.AreEqual(0, Database.Table<StudentCourse>().Count());
    }

    [Test]
    public void QueryCompositePKTable()
    {
        Database.CreateTable<StudentCourse>();

        Database.Insert(new StudentCourse { StudentId = 1, CourseId = 101, Grade = 3.5m });
        Database.Insert(new StudentCourse { StudentId = 1, CourseId = 102, Grade = 4.0m });
        Database.Insert(new StudentCourse { StudentId = 2, CourseId = 101, Grade = 3.8m });

        var student1Courses = Database.Table<StudentCourse>()
            .Where(x => x.StudentId == 1)
            .ToList();

        Assert.AreEqual(2, student1Courses.Count);
        Assert.That(student1Courses.All(c => c.StudentId == 1));

        var course101Students = Database.Table<StudentCourse>()
            .Where(x => x.CourseId == 101)
            .ToList();

        Assert.AreEqual(2, course101Students.Count);
    }

    [Test]
    public void InsertOrReplaceCompositePK()
    {
        Database.CreateTable<StudentCourse>();

        var row = new StudentCourse { StudentId = 1, CourseId = 101, Grade = 3.5m };
        Database.Insert(row);

        var replaced = new StudentCourse { StudentId = 1, CourseId = 101, Grade = 4.0m };
        Database.Insert(replaced, InsertConflictAction.Replace);

        Assert.AreEqual(1, Database.Table<StudentCourse>().Count());
        var found = Database.Find<StudentCourse>(1, 101);
        Assert.That(found, Is.Not.Null);
        Assert.AreEqual(4.0m, found.Grade);
    }

    [Test]
    public void CompositePKWhereSql()
    {
        var map = Database.GetMapping<StudentCourse>();
        Assert.That(map.PKWhereSql, Does.Contain("StudentId"));
        Assert.That(map.PKWhereSql, Does.Contain("CourseId"));
    }
}
