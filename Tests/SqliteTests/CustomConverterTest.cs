using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FourLambda.SQLite.Tests;

[TestFixture]
public class CustomConverterTest : DBTestHarness
{
    public readonly struct RgbaColor(byte red, byte green, byte blue, byte alpha = 255)
    {
        public byte Red { get; } = red;
        public byte Green { get; } = green;
        public byte Blue { get; } = blue;
        public byte Alpha { get; } = alpha;

        public int RawValue => (Alpha << 24) | (Red << 16) | (Green << 8) | Blue;

        public override bool Equals(object? obj) => obj is RgbaColor other && RawValue == other.RawValue;
        public override int GetHashCode() => RawValue;

        [ExcludeFromCodeCoverage]
        public override string ToString() => $"R={Red},G={Green},B={Blue},A={Alpha}";

        public static bool operator ==(RgbaColor left, RgbaColor right) => left.RawValue == right.RawValue;
        public static bool operator !=(RgbaColor left, RgbaColor right) => left.RawValue != right.RawValue;
    }

    public readonly struct Vector3(float x, float y, float z)
    {
        public float X { get; } = x;
        public float Y { get; } = y;
        public float Z { get; } = z;

        public override bool Equals(object? obj) => obj is Vector3 other && X == other.X && Y == other.Y && Z == other.Z;
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        [ExcludeFromCodeCoverage]
        public override string ToString() => $"({X},{Y},{Z})";

        public static bool operator ==(Vector3 left, Vector3 right)
            => left.X == right.X && left.Y == right.Y && left.Z == right.Z;
        public static bool operator !=(Vector3 left, Vector3 right)
            => !(left == right);
    }

    public class ColorItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; }
        public RgbaColor Color { get; set; }
    }

    public class VectorItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Label { get; set; }
        public Vector3 Position { get; set; }
    }

    protected override void InitializeDatabase()
    {
        ValueConverter.AddConverter<RgbaColor>(
            getter: static (statement, index, column, colType) =>
            {
                var raw = SQLite3Native.ColumnInt(statement, index);
                return new RgbaColor(
                    red: (byte)((raw >> 16) & 0xFF),
                    green: (byte)((raw >> 8) & 0xFF),
                    blue: (byte)(raw & 0xFF),
                    alpha: (byte)((raw >> 24) & 0xFF));
            },
            setter: static (statement, index, column, value) =>
                SQLite3Native.BindInt(statement, index, value.RawValue),
            cellType: static _ => SqliteCellType.Integer
        );

        ValueConverter.AddConverter<Vector3>(
            getter: static (statement, index, column, colType) =>
            {
                var text = SQLite3Native.ColumnString(statement, index);
                var parts = text.Split(',');
                return new Vector3(
                    float.Parse(parts[0], CultureInfo.InvariantCulture),
                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                    float.Parse(parts[2], CultureInfo.InvariantCulture));
            },
            setter: static (statement, index, column, value) =>
                SQLite3Native.BindText(statement, index,
                    $"{value.X:F2},{value.Y:F2},{value.Z:F2}", -1, -1),
            cellType: static _ => SqliteCellType.Text
        );

        Database.CreateTable<ColorItem>();
        Database.CreateTable<VectorItem>();
    }

    [Test]
    public void CreateTableWithCustomStruct()
    {
        var mapping = Database.GetMapping<ColorItem>();
        Assert.AreEqual("ColorItem", mapping.TableName);
        Assert.AreEqual(3, mapping.Columns.Length);

        var colMapping = Database.GetMapping<VectorItem>();
        Assert.AreEqual("VectorItem", colMapping.TableName);
        Assert.AreEqual(3, colMapping.Columns.Length);
    }

    [Test]
    public void InsertAndReadRgbaColor()
    {
        var items = new[]
        {
            new ColorItem { Name = "Red",    Color = new RgbaColor(255, 0, 0) },
            new ColorItem { Name = "Green",  Color = new RgbaColor(0, 255, 0) },
            new ColorItem { Name = "Blue",   Color = new RgbaColor(0, 0, 255) },
            new ColorItem { Name = "WhiteA", Color = new RgbaColor(255, 255, 255, 128) },
        };

        foreach (var item in items)
            Assert.AreEqual(1, Database.Insert(item));

        var results = Database.Table<ColorItem>().ToList();
        Assert.AreEqual(items.Length, results.Count);

        for (int i = 0; i < items.Length; i++)
        {
            Assert.AreEqual(items[i].Name, results[i].Name);
            Assert.AreEqual(items[i].Color, results[i].Color);
            Assert.AreEqual(items[i].Color.Red, results[i].Color.Red);
            Assert.AreEqual(items[i].Color.Green, results[i].Color.Green);
            Assert.AreEqual(items[i].Color.Blue, results[i].Color.Blue);
            Assert.AreEqual(items[i].Color.Alpha, results[i].Color.Alpha);
        }
    }

    [Test]
    public void InsertAndReadVector3()
    {
        var items = new[]
        {
            new VectorItem { Label = "Origin", Position = new Vector3(0f, 0f, 0f) },
            new VectorItem { Label = "Up",     Position = new Vector3(0f, 1f, 0f) },
            new VectorItem { Label = "Far",    Position = new Vector3(12.5f, -3.7f, 99.9f) },
        };

        foreach (var item in items)
            Assert.AreEqual(1, Database.Insert(item));

        var results = Database.Table<VectorItem>().ToList();
        Assert.AreEqual(items.Length, results.Count);

        for (int i = 0; i < items.Length; i++)
        {
            Assert.AreEqual(items[i].Label, results[i].Label);
            Assert.AreEqual(items[i].Position.X, results[i].Position.X);
            Assert.AreEqual(items[i].Position.Y, results[i].Position.Y);
            Assert.AreEqual(items[i].Position.Z, results[i].Position.Z);
        }
    }

    [Test]
    public void QueryCustomStructByValue()
    {
        Database.Insert(new ColorItem { Name = "Crimson", Color = new RgbaColor(220, 20, 60) });
        Database.Insert(new ColorItem { Name = "Navy",    Color = new RgbaColor(0, 0, 128) });

        var crimson = Database.Table<ColorItem>()
            .FirstOrDefault(c => c.Name == "Crimson");

        Assert.IsNotNull(crimson);
        Assert.AreEqual("Crimson", crimson.Name);
        Assert.AreEqual(new RgbaColor(220, 20, 60), crimson.Color);
    }

    [Test]
    public void UpdateCustomStruct()
    {
        var original = new ColorItem { Name = "Mutable", Color = new RgbaColor(100, 100, 100) };
        Assert.AreEqual(1, Database.Insert(original));

        original.Color = new RgbaColor(255, 0, 0);
        Assert.AreEqual(1, Database.Update(original));

        var updated = Database.Find<ColorItem>(original.Id);
        Assert.IsNotNull(updated);
        Assert.AreEqual(new RgbaColor(255, 0, 0), updated.Color);
    }

    [Test]
    public void DeleteCustomStruct()
    {
        Database.Insert(new VectorItem { Label = "Keep",   Position = new Vector3(1f, 0f, 0f) });
        var toDelete = new VectorItem { Label = "Remove", Position = new Vector3(0f, 1f, 0f) };
        Assert.AreEqual(1, Database.Insert(toDelete));

        Assert.AreEqual(2, Database.Table<VectorItem>().Count());
        Assert.AreEqual(1, Database.Delete(toDelete));
        Assert.AreEqual(1, Database.Table<VectorItem>().Count());

        var remaining = Database.Table<VectorItem>().First();
        Assert.AreEqual("Keep", remaining.Label);
    }
}
