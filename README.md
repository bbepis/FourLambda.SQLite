<!-- PROJECT LOGO -->
<br />
<div align="center">
<img src="logo.svg" alt="Logo" width="320" height="320">

  <h3 align="center">FourLambda.SQLite</h3>

  <p align="center">
    SQLite bindings for C#
    <br />
    Forked from <a href="https://github.com/praeclarum/sqlite-net">https://github.com/praeclarum/sqlite-net</a>
    <br />
    <a href="https://github.com/othneildrew/Best-README-Template"><strong>Get started »</strong></a>
    <br />
    <br />
    <a href="https://github.com/othneildrew/Best-README-Template">View Demo</a>
    &middot;
    <a href="https://github.com/othneildrew/Best-README-Template/issues/new?labels=bug&template=bug-report---.md">Report Bug</a>
    &middot;
    <a href="https://github.com/othneildrew/Best-README-Template/issues/new?labels=enhancement&template=feature-request---.md">Request Feature</a>
  </p>
</div>

<style>
    table {
        width: 100%;
    }
</style>


## Overview

This fork exists because I have been hitting my head on some longstanding issues in the original package. Since they require very large architectural changes and the original project has been more or less dead for a while, I figured they would be very unlikely to get merged in.

I created this fork for my own personal use, but decided to clean it up and release it for others to use.

Like the upstream version, this package has a lot of benefits:

- Extremely lean
- Straightforward setup with minimal boilerplate, just create table definitions and go from there

This fork also includes a lot of added features and fixed issues:

- Massive performance and allocation improvements
- Fixes executing SQL that contains unicode text [[#226](https://github.com/praeclarum/sqlite-net/issues/226)] [[#1162](https://github.com/praeclarum/sqlite-net/issues/1162)] [[#215](https://github.com/praeclarum/sqlite-net/issues/215)]
- Up-to-date sqlite3 binaries [[#1297](https://github.com/praeclarum/sqlite-net/issues/1297)] [[#1282](https://github.com/praeclarum/sqlite-net/issues/1282)] [[#1288](https://github.com/praeclarum/sqlite-net/issues/1288)]
- Support for composite / multiple column primary keys [[#280](https://github.com/praeclarum/sqlite-net/issues/280)] [[#642](https://github.com/praeclarum/sqlite-net/issues/642)] [[#1101](https://github.com/praeclarum/sqlite-net/issues/1101)]
- Per-column string conversion definitions instead of a rigid global conversion ruleset [[#360](https://github.com/praeclarum/sqlite-net/issues/360)]
- Ability to specify tables as `STRICT` (defined by `[TableAttribute(Strict = true)]`)
- Can automatically infer NULL / NOT NULL constraints from column types in nullable-enabled projects without needing attributes [[#1230](https://github.com/praeclarum/sqlite-net/issues/1230)]
- Fix for issue in .NET 10 / C# 14 where query expressions break due to `Span<>` overload priority [[#1295](https://github.com/praeclarum/sqlite-net/issues/1295)]
- `DateOnly` and `TimeOnly` are now supported data types [[#1156](https://github.com/praeclarum/sqlite-net/issues/1156)]
- Table mappings can be created without needing a backing type via TableMappingBuilder [[#212](https://github.com/praeclarum/sqlite-net/issues/212)] [[#422](https://github.com/praeclarum/sqlite-net/issues/422)] [[#533](https://github.com/praeclarum/sqlite-net/pull/533)]
- Queries can use data classes other than the ones listed in their mapping, for when mappings are constructed at runtime or you don't know the structure in advance [[#390](https://github.com/praeclarum/sqlite-net/issues/390)] [[#725](https://github.com/praeclarum/sqlite-net/issues/725)] [[#85](https://github.com/praeclarum/sqlite-net/pull/85)]
- Structs can be used for query data results [[#1075](https://github.com/praeclarum/sqlite-net/issues/1075)] [[#1266](https://github.com/praeclarum/sqlite-net/pull/1266)]
- Column data type serialization is extensible, so you can now provide your own custom column definitions [[#847](https://github.com/praeclarum/sqlite-net/issues/847)] [[#1285](https://github.com/praeclarum/sqlite-net/issues/1285)] [[#534](https://github.com/praeclarum/sqlite-net/pull/534)] [[#623](https://github.com/praeclarum/sqlite-net/pull/623)]
- Complete rework of public API, with much of the old confusing and redundant API surface unified
- Much needed modernization, refactoring and TLC for the codebase.

## Benchmarks

Here are some benchmarks compared to the upstream sqlite-net fork and the standard Microsoft.Data.Sqlite.Core package:

### Writing

Stats for bulk inserting 500,000 rows:

| Library               |         Time |            Ratio | Allocated      |     Allocated Ratio |
| --------------------- | -----------: | ---------------: | -------------: | ------------------: |
| **FourLambda**        | **321.8 ms** | **1.90x faster** |    **0.02 MB** | **11,620.17x less** |
| SQLitePCL             |     612.3 ms | 1.00x (baseline) |      261.99 MB |    1.00x (baseline) |
| Microsoft.Data.Sqlite |   1,359.4 ms |     2.23x slower |      676.06 MB |          2.58x more |

### Reading

Stats for pulling 500,000 rows using different methods:

| Library               |                                Method |          Time |            Ratio | Allocated      |      Alloc Ratio |
| --------------------- | ------------------------------------- | ------------: | ---------------: | -------------: |   -------------: |
| **FourLambda**        | **Query (into value tuple)**          | **199.24 ms** | **1.94x faster** |   **43.24 MB** |   **3.00x less** |
| **FourLambda**        | **DataReader**                        | **208.00 ms** | **1.87x faster** |   **43.24 MB** |   **3.00x less** |
| **FourLambda**        | **Query (into object)**               | **210.51 ms** | **1.84x faster** |   **82.30 MB** |   **1.57x less** |
| SQLitePCL             | DeferredQuery (into object)           |     387.17 ms | 1.00x (baseline) |      129.18 MB | 1.00x (baseline) |
| Microsoft.Data.Sqlite | DataReader                            |     399.55 ms |     1.03x slower |       43.25 MB |       3.00x less |
| SQLitePCL             | DeferredQuery (into value tuple)      |     451.84 ms |     1.17x slower |      176.06 MB |       1.36x more |

## Breaking Changes

As compared to the upstream `sqlite-pcl` package:

- SQLiteAsyncConnection has been removed, and async versions of functions are instead implemented via extension methods.
  - Note that none of these are *actually* async and use `Task.Run` to offload work from the current thread. It's basically [what the original async implementation does](https://github.com/praeclarum/sqlite-net/blob/master/src/SQLiteAsync.cs#L478-L515) under the hood as well
- Many compiler directives relating to UWP/alternative/older platforms have been removed. .NET 8 and .NET 10 are the main targets for this project
- Implicit index creation is not supported (e.g. using `CreateFlags.ImplicitIndex` or ending property names in `Id`). Manually mark your properties you wish to have indexed with `[Index]`
- `[StoreAsText]` can't be applied to `enum` declarations anymore. Instead, apply them to each property column you wish to have converted
- All connection string settings relating to `StoreDateTimeAsTicks`/`StoreTimeSpanAsTicks` have been removed and are always enabled.
  - No configuration is needed if you are reading from an existing database which stores times as strings; the data reader is smart enough to convert depending on the type of data in the cell being read
  - If you wish to store these values as strings (which required to keep timezone information in `DateTimeOffset`), apply `[StoreAsText]` to the column property.
  - `DateTimeStringFormat` is instead a parameter to the `[StoreAsText]` attribute.
- A lot of lifecycle events and hooks no longer exist as part of API simplification
- Query/QueryScalars/DeferredQuery have been merged into just Query<T>(). If you want to get an IEnumerable of scalars, just pass the scalar type to Query
- `RunInTransaction` has been removed in favor of `CreateTransactionScope`, which mimics ITransactionScope from EF.Core
- Scalar support for `Uri`, `StringBuilder` and `UriBuilder` have been removed. If you need them back, you can add your own custom serialization definitions
- `decimal` values are now stored as text to prevent precision loss. They are still able to be loaded from the database if they are stored as floats or integers, however


## Getting Started

Install the package via NuGet:

```sh
dotnet add package FourLambda.SQLite
```

Define your table classes using attributes to configure columns and keys:

```csharp
using FourLambda.SQLite;

public class Product
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; }
    public decimal Price { get; set; }
}
```

Open a connection and create the table:

```csharp
using var connection = new SQLiteConnection("my_database.db");
connection.CreateTable<Product>();
```

Insert, query, update, and delete records:

```csharp
// Insert a row (returns number of rows affected)
var p = new Product { Name = "Widget", Price = 9.99m };
connection.Insert(p); // p.Id is now populated with the auto-incremented key

// Query using LINQ via Table<T>()
var expensive = connection.Table<Product>().Where(x => x.Price > 5.00).ToList();

// Retrieve by primary key
var found = connection.Find<Product>(p.Id);

// Update an existing row
found.Price = 7.99m;
connection.Update(found);

// Delete a single row by object or primary key
connection.Delete(found);
// or
connection.Delete<Product>(p.Id);

// Batch insert multiple rows
var products = new[] { new Product { Name = "Gadget", Price = 19.99m } };
connection.InsertAll(products);

// Delete all matching a predicate
connection.Table<Product>().Delete(x => x.Price < 10m);
```

## Usage

### Querying

There are multiple ways of retrieving data from a table, all using the same `SQLiteConnection.Query<T>()` method:

```csharp
public class TestData
{
    public int ID { get; set; }

    public string TextValue { get; set; }
}

// Return all data from a table
IEnumerable<TestData> results = connection.Query<TestData>();

// Return using a specific select query
results = connection.Query<TestData>("SELECT * FROM TestData WHERE TextValue = ?", "myValue");

// Return using a predicate expression to filter data
results = connection.Query<TestData>(x => x.ID == 1);
```

If you need to use a table mapping constructed at runtime that doesn't correspond to an existing type, then you can use overloads that accept a `TableMapping` parameter.

The type associated to a SQLite table does not have to be the type that is used for a query. If you provide a table mapping or SQL statement, then the ORM is smart enough to load data into these types as well.

Three additional classes of types are available: scalars, value tuples and adjacent types:

```csharp
// Using a scalar
var results = connection.Query<int>("SELECT ID FROM TestData");

// Using a value tuple
var results = connection.Query<(int id, string textValue)>("SELECT ID, TextValue FROM TestData");

// Using an adjacent type

var mapping = connection.GetMapping<TestData>();

public class AdjacentType
{
    public string TextValue { get; set; }
}

var results = connection.Query<AdjacentType>(mapping);
```

Note that for value tuples, the ordering of the columns is what's used and not the names of the individual tuple elements. (C# does not compile this information into your code)

### Advanced Expressions

For more advanced expressions, you can use the `connection.Table<T>()` query. Certain methods are translated to SQL, such as `.Where()`, `.OrderBy()`, `.Take()`, and `.Skip()`:

```csharp
var results = connection.Table<Product>()
    .Where(x => x.Price > 10 && x.Name.StartsWith("W"))
    .OrderBy(x => x.Name)
    .Skip(5)
    .Take(20)
    .ToList();

// Count without loading all rows
int c = connection.Table<Product>().Count(x => x.Price > 10);

// Single row lookups
var first = connection.Table<Product>().FirstOrDefault(x => x.Name == "Widget");
```

## Table creation & migration

`connection.CreateTable<T>()` will create a table using the specific type as a reference. Only public properties will be considered as columns. Use the table below as a reference to what attributes are available to decorate members with:

| Attribute                                                | Target   | Description                                                                                                                                                                              |
| -------------------------------------------------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `[Table("Name")]`                                        | Class    | Sets the table name. Use `Strict = true` for [STRICT tables](https://sqlite.org/ststricttyped.html), or `WithoutRowId = true` for [WITHOUT ROWID](https://sqlite.org/withoutrowid.html). |
| `[Column("Name")]`                                       | Property | Overrides the column name in the database.                                                                                                                                               |
| `[PrimaryKey]`                                           | Property | Marks a primary key column. Use `.Order` for composite keys.                                                                                                                             |
| `[AutoIncrement]`                                        | Property | Makes the primary key auto-incrementing (int/long only).                                                                                                                                 |
| `[Indexed]` / `[Indexed("Name", order)]`                 | Property | Creates an index. Shared `Name` values group columns into a single multi-column index; `order` defines column position.                                                                  |
| `[Unique]`                                               | Property | Like `[Indexed]` but creates a UNIQUE constraint.                                                                                                                                        |
| `[Ignore]`                                               | Property | Excludes the property from the table mapping.                                                                                                                                            |
| `[Collation("NOCASE")]`                                  | Property | Sets the column collation (`BINARY`, `NOCASE`, or `RTRIM`).                                                                                                                              |
| `[NotNull]`                                              | Property | Enforces NOT NULL explicitly (usually inferred from nullable context).                                                                                                                   |
| `[StoreAsText]` / `[StoreAsText(Format = "yyyy-MM-dd")]` | Property | Stores the value as text instead of its native type. Useful for `DateTimeOffset`, `DateOnly`, custom formatting, or enums stored per-property.                                           |

Example combining several attributes:

```csharp
[Table("Products", Strict = true)]
public class Product
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique, MaxLength(100), Collation("NOCASE")]
    public string Name { get; set; }

    [Indexed("IX_CategoryPrice", 1)]
    public string Category { get; set; }

    [Indexed("IX_CategoryPrice", 2)]
    public decimal Price { get; set; }

    [StoreAsText(Format = "yyyy-MM-dd")]
    public DateOnly CreatedDate { get; set; }

    [Ignore]
    public string ComputedDisplay => $"{Name} ({Price:C})";
}
```

#### Migration

`CreateTable<T>()` not only creates tables, but also handles a limited amount of migration if the schema differs.

It is however extremely limited; it only supports adding new columns to existing tables, which always get placed at the end. Modifying or removing existing columns will not transfer those changes to the table.

Due to limitations with SQLite, you also cannot mark new columns as primary keys or append them to composite primary key definitions. 

#### Nullable Reference Types

In nullable-enabled projects, NOT NULL constraints are inferred from the property type:

```csharp
public class Item
{
    [PrimaryKey]
    public int Id { get; set; }       // NOT NULL (value type)

    public string Name { get; set; }  // NOT NULL (non-nullable ref type)

    public string? Description { get; set; } // NULLABLE (nullable ref type)
}

```

Use `CreateFlags.ImplicitNullable` to disable this inference and treat all reference values as nullable. Alternatively, any columns explicitly marked with `[NotNull]` will always be treated as non-nullable.

### Custom Scalar Types

You can register custom type converters using `ValueConverter.AddConverter<T>()`. Note that this requires working with the low-level `SQLite3Native` functions.

After registering, the type is able to be used as a column type, and as a scalar type when reading with `Query<T>()` and `ExecuteScalar<T>()`.

Converters are registered globally and persist for the lifetime of the application. Existing converters can be replaced with `AddConverter<T>()`, including base types, so take care to not break too much.

For structs and value types, the library automatically handles `Nullable<T>` wrapping, so you only need to register a converter for the underlying type.

Here is an example that registers `MyCustomType` as a scalar type.

```csharp
using FourLambda.SQLite;

// Register a converter for a custom type
ValueConverter.AddConverter<MyCustomType>(
    // Getter: reads a value from a statement column
    getter: (statement, index, column, colType) =>
    {
        var text = SQLite3Native.ColumnString(statement, index);
        return MyCustomType.Parse(text);
    },
    // Setter: binds a value to a statement parameter
    setter: (statement, index, column, value) =>
    {
        SQLite3Native.BindText(statement, index, value.ToString(), -1, -1);
    },
    // Cell type: determines the SQLite storage type for columns of this converter
    cellType: _ => SqliteCellType.Text
);
```

See the tests or base type implementations for more examples.

## License

Distributed under the MIT License, original version authored by `Krueger Systems, Inc.`. See `LICENSE.txt` for more information.