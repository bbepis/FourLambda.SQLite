using System.Runtime.CompilerServices;

namespace FourLambda.SQLite;

public static class ValueConverter
{
	public interface IGenericConverterDefinition
	{
		Func<ColumnDefinition?, SqliteCellType> DetermineCellType { get; }

		object? StatementGetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, SQLite3Native.ColType colType);
		void StatementSetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, object value);

		Action<TRowObject, Sqlite3Statement, int> StatementSetterGeneric<TRowObject>(TableColumn column);
	}

	public interface IConverterDefinition<T> : IGenericConverterDefinition
	{
		public Type ClrType { get; }

		public Action<Sqlite3Statement, int, TableColumn?, T> StatementSetter { get; }
		public Func<Sqlite3Statement, int, TableColumn?, SQLite3Native.ColType, T?> StatementGetter { get; }
	}

	public class ConverterDefinition<T> : IConverterDefinition<T> where T : class
	{
		public Type ClrType { get; } = typeof(T);

		public Action<Sqlite3Statement, int, TableColumn?, T> StatementSetter { get; init; }
		public Func<Sqlite3Statement, int, TableColumn?, SQLite3Native.ColType, T?> StatementGetter { get; init; }
		public Func<ColumnDefinition?, SqliteCellType> DetermineCellType { get; init; }

		public object? StatementGetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, SQLite3Native.ColType colType)
			=> StatementGetter(statement, index, column, colType);
		public void StatementSetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, object value)
			=> StatementSetter(statement, index, column, (T)value);

		public Action<TRowObject, Sqlite3Statement, int> StatementSetterGeneric<TRowObject>(TableColumn column)
		{
			var setProperty = (Action<TRowObject, T?>)Delegate.CreateDelegate(
				typeof(Action<TRowObject, T>),
				null,
				column.PropertyInfo.GetSetMethod());

			return (o, stmt, i) => {
				var colType = SQLite3Native.ColumnType(stmt, i);
				var value = StatementGetter.Invoke(stmt, i, column, colType);

				if (colType != SQLite3Native.ColType.Null && value != null)
				{
					setProperty.Invoke(o, value);
				}
			};
		}
	}

	public class StructConverterDefinition<T> : IConverterDefinition<T> where T : struct
	{
		public Type ClrType { get; } = typeof(T);

		public Action<Sqlite3Statement, int, TableColumn?, T> StatementSetter { get; init; }
		public Func<Sqlite3Statement, int, TableColumn?, SQLite3Native.ColType, T> StatementGetter { get; init; }
		public Func<ColumnDefinition?, SqliteCellType> DetermineCellType { get; init; }

		public object? StatementGetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, SQLite3Native.ColType colType)
			=> StatementGetter(statement, index, column, colType);
		public void StatementSetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, object value)
			=> StatementSetter(statement, index, column, (T)value);

		public Action<TRowObject, Sqlite3Statement, int> StatementSetterGeneric<TRowObject>(TableColumn column)
		{
			var isNullable = column.PropertyInfo.PropertyType.Name == "Nullable`1";

			if (isNullable)
			{
				var setNullableProperty = (Action<TRowObject, T?>)Delegate.CreateDelegate(
					typeof(Action<TRowObject, T?>),
					null,
					column.PropertyInfo.GetSetMethod());

				return (o, stmt, i) => {
					var colType = SQLite3Native.ColumnType(stmt, i);
					var value = StatementGetter.Invoke(stmt, i, column, colType);

					if (colType != SQLite3Native.ColType.Null)
					{
						setNullableProperty.Invoke(o, value);
					}
				};
			}

			var setProperty = (Action<TRowObject, T>)Delegate.CreateDelegate(
				typeof(Action<TRowObject, T>),
				null,
				column.PropertyInfo.GetSetMethod());

			return (o, stmt, i) => {
				var colType = SQLite3Native.ColumnType(stmt, i);
				var value = StatementGetter.Invoke(stmt, i, column, colType);

				if (colType != SQLite3Native.ColType.Null)
				{
					setProperty.Invoke(o, value);
				}
			};
		}
	}

	public class EnumConverterDefinition<T> : IConverterDefinition<T> where T : struct, Enum
	{
		public Type ClrType { get; } = typeof(T);

		private static readonly bool CanFastCast = Enum.GetUnderlyingType(typeof(T)) == typeof(int);

		public Action<Sqlite3Statement, int, TableColumn?, T> StatementSetter { get; init; } =
			static (statement, index, column, value) =>
			{
				if (column?.StoreAsText == true)
				{
					var stringValue = Enum.GetName(column.ColumnType, value);
					SQLite3Native.BindText(statement, index, stringValue, -1, -1);
				}
				else
				{
					var enumIntValue = Convert.ToInt64(value);
					SQLite3Native.BindInt64(statement, index, enumIntValue);
				}
			};

		public Func<Sqlite3Statement, int, TableColumn?, SQLite3Native.ColType, T> StatementGetter { get; init; } =
			static (statement, index, column, colType) =>
			{
				if (colType == SQLite3Native.ColType.Text)
				{
					var stringValue = SQLite3Native.ColumnString(statement, index);
					return Enum.Parse<T>(stringValue, true);
				}

				var rawValue = SQLite3Native.ColumnInt(statement, index);

				return CanFastCast ? Unsafe.As<int, T>(ref rawValue) : (T)Enum.ToObject(typeof(T), rawValue);
			};

		public Func<ColumnDefinition?, SqliteCellType> DetermineCellType { get; init; }
			= static column => column?.StoreAsText == true ? SqliteCellType.Text : SqliteCellType.Integer;

		public object? StatementGetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, SQLite3Native.ColType colType)
			=> StatementGetter(statement, index, column, colType);
		public void StatementSetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, object value)
			=> StatementSetter(statement, index, column, (T)value);

		public Action<TRowObject, Sqlite3Statement, int> StatementSetterGeneric<TRowObject>(TableColumn column)
		{
			var isNullable = column.PropertyInfo.PropertyType.Name == "Nullable`1";

			if (isNullable)
			{
				var setNullableProperty = (Action<TRowObject, T?>)Delegate.CreateDelegate(
					typeof(Action<TRowObject, T?>),
					null,
					column.PropertyInfo.GetSetMethod());

				return (o, stmt, i) => {
					var colType = SQLite3Native.ColumnType(stmt, i);
					var value = StatementGetter.Invoke(stmt, i, column, colType);

					if (colType != SQLite3Native.ColType.Null)
					{
						setNullableProperty.Invoke(o, value);
					}
				};
			}

			var setProperty = (Action<TRowObject, T>)Delegate.CreateDelegate(
				typeof(Action<TRowObject, T>),
				null,
				column.PropertyInfo.GetSetMethod());

			return (o, stmt, i) => {
				var colType = SQLite3Native.ColumnType(stmt, i);
				var value = StatementGetter.Invoke(stmt, i, column, colType);

				if (colType != SQLite3Native.ColType.Null)
				{
					setProperty.Invoke(o, value);
				}
			};
		}
	}

	private static readonly Dictionary<Type, IGenericConverterDefinition> ConverterDefinitions = new();

	public static bool TryGetConverterDefinition(Type type, [NotNullWhen(true)] out IGenericConverterDefinition? definition)
	{
		lock (ConverterDefinitions)
		{
			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				type = type.GenericTypeArguments[0];
			}

			var result = ConverterDefinitions.TryGetValue(type, out definition);

			if (!result && type.IsEnum)
			{
				definition = (IGenericConverterDefinition)Activator.CreateInstance(typeof(EnumConverterDefinition<>).MakeGenericType(type))!;
				ConverterDefinitions[type] = definition;
				return true;
			}

			return result;
		}
	}

	public static bool TryGetConverterDefinition<T>([NotNullWhen(true)] out IConverterDefinition<T>? definition)
	{
		var result = TryGetConverterDefinition(typeof(T), out var generic);

		definition = result ? (IConverterDefinition<T>)generic! : null;

		return result;
	}

	public static void AddConverter<T>(IConverterDefinition<T> converter)
	{
		lock (ConverterDefinitions)
			ConverterDefinitions[converter.ClrType] = converter;
	}

	static ValueConverter()
	{
		AddConverter(new ConverterDefinition<string>
		{
			StatementGetter = static (statement, index, column, colType) => SQLite3Native.ColumnString(statement, index),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindText(statement, index, value, -1, -1),
			DetermineCellType = static _ => SqliteCellType.Text
		});

		AddConverter(new StructConverterDefinition<int>
		{
			StatementGetter = static (statement, index, column, colType) => SQLite3Native.ColumnInt(statement, index),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindInt(statement, index, value),
			DetermineCellType = _ => SqliteCellType.Integer
		});

		AddConverter(new StructConverterDefinition<bool>
		{
			StatementGetter = static (statement, index, column, colType) => SQLite3Native.ColumnInt(statement, index) == 1,
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindInt(statement, index, value ? 1 : 0),
			DetermineCellType = static _ => SqliteCellType.Integer
		});

		AddConverter(new StructConverterDefinition<double>
		{
			StatementGetter = static (statement, index, column, colType) => SQLite3Native.ColumnDouble(statement, index),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindDouble(statement, index, value),
			DetermineCellType = static _ => SqliteCellType.Real
		});

		AddConverter(new StructConverterDefinition<float>
		{
			StatementGetter = static (statement, index, column, colType) => (float)SQLite3Native.ColumnDouble(statement, index),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindDouble(statement, index, value),
			DetermineCellType = static _ => SqliteCellType.Real
		});

		AddConverter(new StructConverterDefinition<long>
		{
			StatementGetter = static (statement, index, column, colType) => SQLite3Native.ColumnInt64(statement, index),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindInt64(statement, index, value),
			DetermineCellType = static _ => SqliteCellType.Integer
		});

		AddConverter(new StructConverterDefinition<ulong>
		{
			StatementGetter = static (statement, index, column, colType) => (ulong)SQLite3Native.ColumnInt64(statement, index),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindInt64(statement, index, (long)value),
			DetermineCellType = static _ => SqliteCellType.Integer
		});

		AddConverter(new StructConverterDefinition<uint>
		{
			StatementGetter = static (statement, index, column, colType) => (uint)SQLite3Native.ColumnInt64(statement, index),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindInt64(statement, index, (long)value),
			DetermineCellType = static _ => SqliteCellType.Integer
		});

		AddConverter(new StructConverterDefinition<decimal>
		{
			StatementGetter = static (statement, index, column, colType) => (decimal)SQLite3Native.ColumnDouble(statement, index),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindDouble(statement, index, (double)value),
			DetermineCellType = static _ => SqliteCellType.Real
		});

		AddConverter(new StructConverterDefinition<byte>
		{
			StatementGetter = static (statement, index, column, colType) => (byte)SQLite3Native.ColumnInt(statement, index),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindInt(statement, index, value),
			DetermineCellType = static _ => SqliteCellType.Integer
		});

		AddConverter(new StructConverterDefinition<ushort>
		{
			StatementGetter = static (statement, index, column, colType) => (ushort)SQLite3Native.ColumnInt(statement, index),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindInt(statement, index, value),
			DetermineCellType = static _ => SqliteCellType.Integer
		});

		AddConverter(new StructConverterDefinition<short>
		{
			StatementGetter = static (statement, index, column, colType) => (short)SQLite3Native.ColumnInt(statement, index),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindInt(statement, index, value),
			DetermineCellType = static _ => SqliteCellType.Integer
		});

		AddConverter(new StructConverterDefinition<sbyte>
		{
			StatementGetter = static (statement, index, column, colType) => (sbyte)SQLite3Native.ColumnInt(statement, index),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindInt(statement, index, value),
			DetermineCellType = static _ => SqliteCellType.Integer
		});

		AddConverter(new ConverterDefinition<byte[]>
		{
			StatementGetter = static (statement, index, column, colType) => SQLite3Native.ColumnByteArray(statement, index),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindBlob(statement, index, value, value.Length, -1),
			DetermineCellType = static _ => SqliteCellType.Blob
		});

		AddConverter(new StructConverterDefinition<Guid>
		{
			StatementGetter = static (statement, index, column, colType) => new Guid(SQLite3Native.ColumnString(statement, index)),
			StatementSetter = static (statement, index, column, value) => SQLite3Native.BindText(statement, index, value.ToString(), -1, -1),
			DetermineCellType = static _ => SqliteCellType.Text
		});

		AddConverter(new StructConverterDefinition<TimeSpan>
		{
			StatementGetter = static (statement, index, column, colType) =>
			{
				if (colType == SQLite3Native.ColType.Text)
				{
					var text = SQLite3Native.ColumnString(statement, index);
					TimeSpan resultTime;

					if (column?.StoreAsText == true && column.StoreAsTextFormat != null)
					{
						if (column.StoreAsTextFormat != null
						    && TimeSpan.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, TimeSpanStyles.None, out resultTime))
							return resultTime;
					}

					if (TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, TimeSpanStyles.None, out resultTime))
						return resultTime;

					return TimeSpan.Parse(text);
				}
				
				return new TimeSpan(SQLite3Native.ColumnInt64(statement, index));
			},
			StatementSetter = static (statement, index, column, value) =>
			{
				if (column?.StoreAsText == true)
				{
					var fmt = column.StoreAsTextFormat ?? "c";
					SQLite3Native.BindText(statement, index, value.ToString(fmt), -1, -1);
				}
				else
				{
					SQLite3Native.BindInt64(statement, index, value.Ticks);
				}
			},
			DetermineCellType = static column => column?.StoreAsText == true ? SqliteCellType.Text : SqliteCellType.Integer
		});

		AddConverter(new StructConverterDefinition<DateTime>
		{
			StatementGetter = static (statement, index, column, colType) =>
			{
				if (colType == SQLite3Native.ColType.Text)
				{
					var text = SQLite3Native.ColumnString(statement, index);

					if (column?.StoreAsText == true && column.StoreAsTextFormat != null)
					{
						if (DateTime.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var resultTime))
							return resultTime;
					}

					return DateTime.Parse(text);
				}

				return new DateTime(SQLite3Native.ColumnInt64(statement, index));
			},
			StatementSetter = static (statement, index, column, value) =>
			{
				if (column?.StoreAsText == true)
				{
					var fmt = column.StoreAsTextFormat ?? "o";
					SQLite3Native.BindText(statement, index, value.ToString(fmt), -1, -1);
				}
				else
				{
					SQLite3Native.BindInt64(statement, index, value.Ticks);
				}
			},
			DetermineCellType = static column => column?.StoreAsText == true ? SqliteCellType.Text : SqliteCellType.Integer
		});

		AddConverter(new StructConverterDefinition<DateTimeOffset>
		{
			StatementGetter = static (statement, index, column, colType) =>
			{
				if (colType == SQLite3Native.ColType.Text)
				{
					var text = SQLite3Native.ColumnString(statement, index);

					if (column?.StoreAsText == true && column.StoreAsTextFormat != null)
					{
						if (DateTimeOffset.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var resultTime))
							return resultTime;
					}
						
					return DateTimeOffset.Parse(text);
				}
					
				return new DateTimeOffset(SQLite3Native.ColumnInt64(statement, index), TimeSpan.Zero);
			},
			StatementSetter = static (statement, index, column, value) =>
			{
				if (column?.StoreAsText == true)
				{
					var fmt = column.StoreAsTextFormat ?? "o";
					SQLite3Native.BindText(statement, index, value.ToString(fmt), -1, -1);
				}
				else
				{
					SQLite3Native.BindInt64(statement, index, value.UtcTicks);
				}
			},
			DetermineCellType = static column => column?.StoreAsText == true ? SqliteCellType.Text : SqliteCellType.Integer
		});

		AddConverter(new StructConverterDefinition<DateOnly>
		{
			StatementGetter = static (statement, index, column, colType) =>
			{
				if (colType == SQLite3Native.ColType.Text)
				{
					var text = SQLite3Native.ColumnString(statement, index);

					if (column?.StoreAsText == true && column.StoreAsTextFormat != null)
					{
						if (DateOnly.TryParseExact(text, column.StoreAsTextFormat,
							    CultureInfo.InvariantCulture, DateTimeStyles.None, out var resultTime))
							return resultTime;
					}
					
					return DateOnly.Parse(text);
				}

				return DateOnly.FromDateTime(new DateTime(SQLite3Native.ColumnInt64(statement, index)));
			},
			StatementSetter = static (statement, index, column, value) =>
			{
				if (column?.StoreAsText == true)
				{
					var fmt = column.StoreAsTextFormat ?? "o";
					SQLite3Native.BindText(statement, index, value.ToString(fmt), -1, -1);
				}
				else
				{
					SQLite3Native.BindInt64(statement, index, value.ToDateTime(TimeOnly.MinValue).Ticks);
				}
			},
			DetermineCellType = static column => column?.StoreAsText == true ? SqliteCellType.Text : SqliteCellType.Integer
		});

		AddConverter(new StructConverterDefinition<TimeOnly>
		{
			StatementGetter = static (statement, index, column, colType) =>
			{
				if (colType == SQLite3Native.ColType.Text)
				{
					var text = SQLite3Native.ColumnString(statement, index);

					if (column?.StoreAsText == true && column.StoreAsTextFormat != null)
					{
						if (TimeOnly.TryParseExact(text, column.StoreAsTextFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var resultTime))
							return resultTime;
					}

					return TimeOnly.Parse(text);
				}
				return new TimeOnly(SQLite3Native.ColumnInt64(statement, index));
			},
			StatementSetter = static (statement, index, column, value) =>
			{
				if (column?.StoreAsText == true)
				{
					var fmt = column.StoreAsTextFormat ?? "o";
					SQLite3Native.BindText(statement, index, value.ToString(fmt), -1, -1);
				}
				else
				{
					SQLite3Native.BindInt64(statement, index, value.Ticks);
				}
			},
			DetermineCellType = static column => column?.StoreAsText == true ? SqliteCellType.Text : SqliteCellType.Integer
		});

		//AddConverter(new ConverterDefinition<object>
		//{
		//	StatementGetter = static (statement, index, column, colType) =>
		//	{
		//		return colType switch
		//		{
		//			SQLite3Native.ColType.Integer => SQLite3Native.ColumnInt64(statement, index),
		//			SQLite3Native.ColType.Real => SQLite3Native.ColumnDouble(statement, index),
		//			SQLite3Native.ColType.Text => SQLite3Native.ColumnString(statement, index),
		//			SQLite3Native.ColType.Blob => SQLite3Native.ColumnByteArray(statement, index),
		//			_ => null
		//		};
		//	},
		//	StatementSetter = static (statement, index, column, value) =>
		//	{
		//		switch (value)
		//		{
		//			case int i:
		//				SQLite3Native.BindInt(statement, index, i);
		//				break;
		//			case string s:
		//				SQLite3Native.BindText(statement, index, s, -1, -1);
		//				break;
		//			case long l:
		//				SQLite3Native.BindInt64(statement, index, l);
		//				break;
		//			case double d:
		//				SQLite3Native.BindDouble(statement, index, d);
		//				break;
		//			case byte[] b:
		//				SQLite3Native.BindBlob(statement, index, b, b.Length, -1);
		//				break;
		//			default:
		//				throw new NotSupportedException($"Cannot store object type: {value.GetType()}");
		//		}
		//	},
		//	DetermineCellType = static _ => SqliteCellType.Any
		//});
	}
}