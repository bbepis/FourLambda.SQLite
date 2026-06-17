<div align="center">
  <h3 align="center">FourLambda.SQLite</h3>

  <p align="center">
    SQLite bindings for C#
    <br />
    Forked from <a href="https://github.com/praeclarum/sqlite-net">https://github.com/praeclarum/sqlite-net</a>
    <br />
    <br />
    <a href="https://github.com/bbepis/FourLambda.Sqlite"><strong>Documentation »</strong></a>
    <br />
  </p>
</div>



## Overview

This package is a simple, high-performance alternative to `sqlite-net` and `Microsoft.Data.Sqlite` with up to **2x** faster insertion and row reading.

It handles table creation, migration, and general data insertion/updating/deletion, all in one simple library.


## Getting Started

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