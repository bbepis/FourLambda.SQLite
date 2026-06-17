using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace FourLambda.SQLite;

/// <summary>
/// Provides type converters for mapping between CLR types and SQLite storage representations.
/// Register custom converters using <see cref="AddConverter{T}(IConverterDefinition{T})"/> or the overload accepting getter/setter delegates.
/// Built-in converters exist for common types such as strings, numerics, dates, and GUIDs.
/// </summary>
public static class ValueConverter
{
	/// <summary>
	/// Delegate that reads a single column from a <see cref="Sqlite3Statement"/> directly into a row object reference.
	/// </summary>
	/// <typeparam name="TRowObject">The containing row type.</typeparam>
	/// <param name="row">Reference to the row object to populate.</param>
	/// <param name="statement">The prepared statement to read from.</param>
	/// <param name="index">Zero-based column index.</param>
	public delegate void DirectStatementAccessorDelegate<TRowObject>(ref TRowObject row, Sqlite3Statement statement, int index);

	/// <summary>
	/// Delegate that determines the SQLite cell type for a given column definition.
	/// </summary>
	/// <param name="column">The column metadata, or <c>null</c>.</param>
	/// <returns>The SQLite cell type to use when storing values.</returns>
	public delegate SqliteCellType DetermineCellTypeFunc(ColumnDefinition? column);

	/// <summary>
	/// Delegate that binds a value of type <typeparamref name="T"/> to a statement parameter.
	/// </summary>
	/// <typeparam name="T">The CLR type being bound.</typeparam>
	/// <param name="statement">The prepared statement.</param>
	/// <param name="index">One-based parameter index.</param>
	/// <param name="column">Optional column metadata for context-aware binding.</param>
	/// <param name="value">The value to bind.</param>
	public delegate void StatementSetterFunc<T>(Sqlite3Statement statement, int index, TableColumn? column, T value);

	/// <summary>
	/// Delegate that reads a value of type <typeparamref name="T"/> from a statement column.
	/// </summary>
	/// <typeparam name="T">The CLR type to read.</typeparam>
	/// <param name="statement">The prepared statement.</param>
	/// <param name="index">Zero-based column index.</param>
	/// <param name="column">Optional column metadata for context-aware reading.</param>
	/// <param name="columnType">The actual SQLite column type reported by the native layer.</param>
	/// <returns>The deserialized value.</returns>
	public delegate T StatementGetterFunc<T>(Sqlite3Statement statement, int index, TableColumn? column, SQLite3Native.ColType columnType);

	private delegate void ActionRef<TTarget, TValue>(ref TTarget target, TValue value);

	/// <summary>
	/// Non-generic contract for a registered type converter. Used internally when the target type is not known at compile time.
	/// </summary>
	public interface IGenericConverterDefinition
	{
		/// <summary>Gets the delegate that determines the SQLite cell type for columns using this converter.</summary>
		DetermineCellTypeFunc DetermineCellType { get; }

		internal object? StatementGetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, SQLite3Native.ColType colType);
		internal void StatementSetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, object value);

		internal DirectStatementAccessorDelegate<TRowObject> DirectStatementGetter<TRowObject>(TableColumn column);
		internal DirectStatementAccessorDelegate<TRowObject> DirectStatementGetter<TRowObject>(FieldInfo fieldInfo);
		internal DirectStatementAccessorDelegate<TRowObject> DirectStatementSetter<TRowObject>(TableColumn column);
	}

	/// <summary>
	/// Generic contract for a registered type converter with strongly-typed getter and setter delegates.
	/// </summary>
	/// <typeparam name="T">The CLR type this converter handles.</typeparam>
	public interface IConverterDefinition<T> : IGenericConverterDefinition
	{
		/// <summary>
		/// Gets the CLR type this converter is registered for.
		/// </summary>
		public Type ClrType { get; }

		/// <summary>
		/// Gets the delegate that binds a value to a statement parameter.
		/// </summary>
		public StatementSetterFunc<T> StatementSetter { get; }

		/// <summary>
		/// Gets the delegate that reads a value from a statement column.
		/// </summary>
		public StatementGetterFunc<T> StatementGetter { get; }
	}

	private class ConverterDefinition<T>(StatementGetterFunc<T> getter, StatementSetterFunc<T> setter, DetermineCellTypeFunc cellType) : IConverterDefinition<T> where T : class
	{
		public Type ClrType { get; } = typeof(T);

		public StatementSetterFunc<T> StatementSetter { get; } = setter;
		public StatementGetterFunc<T> StatementGetter { get; } = getter;
		public DetermineCellTypeFunc DetermineCellType { get; } = cellType;

		object? IGenericConverterDefinition.StatementGetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, SQLite3Native.ColType colType)
			=> StatementGetter(statement, index, column, colType);
		void IGenericConverterDefinition.StatementSetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, object value)
			=> StatementSetter(statement, index, column, (T)value);

		DirectStatementAccessorDelegate<TRowObject> IGenericConverterDefinition.DirectStatementGetter<TRowObject>(TableColumn column)
		{
			var setProperty = (Action<TRowObject, T?>)Delegate.CreateDelegate(
				typeof(Action<TRowObject, T>),
				null,
				column.PropertyInfo.GetSetMethod());

			return (ref TRowObject row, Sqlite3Statement statement, int index) => {
				var colType = SQLite3Native.ColumnType(statement, index);
				var value = StatementGetter.Invoke(statement, index, column, colType);

				if (colType != SQLite3Native.ColType.Null && value != null)
				{
					setProperty.Invoke(row, value);
				}
			};
		}

		public DirectStatementAccessorDelegate<TRowObject> DirectStatementGetter<TRowObject>(FieldInfo fieldInfo)
		{
			ActionRef<TRowObject, T> setProperty = ILHandler.CreateILSetter<TRowObject, T>(fieldInfo);

			return (ref TRowObject row, Sqlite3Statement statement, int index) => {
				var colType = SQLite3Native.ColumnType(statement, index);
				var value = StatementGetter.Invoke(statement, index, null, colType);

				if (colType != SQLite3Native.ColType.Null && value != null)
				{
					setProperty.Invoke(ref row, value);
				}
			};
		}

		DirectStatementAccessorDelegate<TRowObject> IGenericConverterDefinition.DirectStatementSetter<TRowObject>(TableColumn column)
		{
			var getProperty = (Func<TRowObject, T?>)Delegate.CreateDelegate(
				typeof(Func<TRowObject, T?>),
				null,
				column.PropertyInfo.GetGetMethod());

			return (ref TRowObject row, Sqlite3Statement statement, int index) =>
			{
				var value = getProperty(row);

				if (value != null)
					StatementSetter(statement, index, column, value);
				else
					SQLite3Native.BindNull(statement, index);
			};
		}
	}

	private class StructConverterDefinition<T>(StatementGetterFunc<T> getter, StatementSetterFunc<T> setter, DetermineCellTypeFunc cellType) : IConverterDefinition<T> where T : struct
	{
		public Type ClrType { get; } = typeof(T);

		public StatementSetterFunc<T> StatementSetter { get; } = setter;
		public StatementGetterFunc<T> StatementGetter { get; } = getter;
		public DetermineCellTypeFunc DetermineCellType { get; } = cellType;

		public object? StatementGetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, SQLite3Native.ColType colType)
			=> StatementGetter(statement, index, column, colType);
		public void StatementSetterBoxed(Sqlite3Statement statement, int index, TableColumn? column, object value)
			=> StatementSetter(statement, index, column, (T)value);

		DirectStatementAccessorDelegate<TRowObject> IGenericConverterDefinition.DirectStatementGetter<TRowObject>(TableColumn column)
		{
			var isNullable = column.PropertyInfo.PropertyType.Name == "Nullable`1";

			if (isNullable)
			{
				var setNullableProperty = (Action<TRowObject, T?>)Delegate.CreateDelegate(
					typeof(Action<TRowObject, T?>),
					null,
					column.PropertyInfo.GetSetMethod());

				return (ref TRowObject row, Sqlite3Statement statement, int index) =>
				{
					var colType = SQLite3Native.ColumnType(statement, index);
					var value = StatementGetter.Invoke(statement, index, column, colType);

					if (colType != SQLite3Native.ColType.Null)
					{
						setNullableProperty.Invoke(row, value);
					}
				};
			}

			var setProperty = (Action<TRowObject, T>)Delegate.CreateDelegate(
				typeof(Action<TRowObject, T>),
				null,
				column.PropertyInfo.GetSetMethod());

			return (ref TRowObject row, Sqlite3Statement statement, int index) =>
			{
				var colType = SQLite3Native.ColumnType(statement, index);
				var value = StatementGetter.Invoke(statement, index, column, colType);

				if (colType != SQLite3Native.ColType.Null)
				{
					setProperty.Invoke(row, value);
				}
			};
		}

		DirectStatementAccessorDelegate<TRowObject> IGenericConverterDefinition.DirectStatementGetter<TRowObject>(FieldInfo fieldInfo)
		{
			ActionRef<TRowObject, T> setProperty = ILHandler.CreateILSetter<TRowObject, T>(fieldInfo);

			return (ref TRowObject row, Sqlite3Statement statement, int index) => {
				var colType = SQLite3Native.ColumnType(statement, index);
				var value = StatementGetter.Invoke(statement, index, null, colType);

				if (colType != SQLite3Native.ColType.Null)
				{
					setProperty.Invoke(ref row, value);
				}
			};
		}

		DirectStatementAccessorDelegate<TRowObject> IGenericConverterDefinition.DirectStatementSetter<TRowObject>(TableColumn column)
		{
			var isNullable = column.PropertyInfo.PropertyType.Name == "Nullable`1";

			if (isNullable)
			{
				var getProperty = (Func<TRowObject, T?>)Delegate.CreateDelegate(
					typeof(Func<TRowObject, T?>),
					null,
					column.PropertyInfo.GetGetMethod());

				return (ref TRowObject row, Sqlite3Statement statement, int index) =>
				{
					var value = getProperty(row);

					if (value.HasValue)
						StatementSetter(statement, index, column, value.Value);
					else
						SQLite3Native.BindNull(statement, index);
				};
			}
			else
			{
				var getProperty = (Func<TRowObject, T>)Delegate.CreateDelegate(
					typeof(Func<TRowObject, T>),
					null,
					column.PropertyInfo.GetGetMethod());

				return (ref TRowObject row, Sqlite3Statement statement, int index) => StatementSetter(statement, index, column, getProperty(row));
			}
		}
	}

	private class EnumConverterDefinition<T> : StructConverterDefinition<T> where T : struct, Enum
	{
		private static readonly bool CanFastCast = Enum.GetUnderlyingType(typeof(T)) == typeof(int);

		private EnumConverterDefinition(StatementGetterFunc<T> getter, StatementSetterFunc<T> setter, DetermineCellTypeFunc cellType)
			: base(getter, setter, cellType) { }

		public static EnumConverterDefinition<T> Create()
		{
			return new EnumConverterDefinition<T>(
				setter: static (statement, index, column, value) =>
				{
					if (column?.StoreAsText == true)
					{
						var stringValue = Enum.GetName(column.ColumnType, value);
						SQLite3Native.BindText(statement, index, stringValue, -1, -1);
					}
					else if (CanFastCast)
					{
						SQLite3Native.BindInt(statement, index, Unsafe.As<T, int>(ref value));
					}
					else
					{
						var enumIntValue = Convert.ToInt64(value);
						SQLite3Native.BindInt64(statement, index, enumIntValue);
					}
				},

				getter: static (statement, index, column, colType) =>
				{
					if (colType == SQLite3Native.ColType.Text)
					{
						var stringValue = SQLite3Native.ColumnString(statement, index);
						return Enum.Parse<T>(stringValue, true);
					}

					var rawValue = SQLite3Native.ColumnInt(statement, index);

					return CanFastCast ? Unsafe.As<int, T>(ref rawValue) : (T)Enum.ToObject(typeof(T), rawValue);
				},

				cellType: static column => column?.StoreAsText == true ? SqliteCellType.Text : SqliteCellType.Integer
			);
		}
	}

	private static readonly Dictionary<Type, IGenericConverterDefinition> ConverterDefinitions = new();

	internal static bool TryGetConverterDefinition(Type type, [NotNullWhen(true)] out IGenericConverterDefinition? definition)
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
				var method = typeof(EnumConverterDefinition<>).MakeGenericType(type)
					.GetMethod(nameof(EnumConverterDefinition<>.Create), BindingFlags.Static | BindingFlags.Public);

				definition = (IGenericConverterDefinition)method.Invoke(null, null)!;
				ConverterDefinitions[type] = definition;
				return true;
			}

			return result;
		}
	}

	internal static IConverterDefinition<T> GetConverterDefinition<T>()
	{
		if (TryGetConverterDefinition(typeof(T), out var generic))
			return (IConverterDefinition<T>)generic!;

		throw new ArgumentOutOfRangeException(nameof(T), "Type does not exist");
	}

	internal static bool TryGetConverterDefinition<T>([NotNullWhen(true)] out IConverterDefinition<T>? definition)
	{
		var result = TryGetConverterDefinition(typeof(T), out var generic);

		definition = result ? (IConverterDefinition<T>)generic! : null;

		return result;
	}

	/// <summary>
	/// Registers a custom converter for type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The CLR type to convert.</typeparam>
	/// <param name="converter">A converter implementation providing getter, setter, and cell-type logic.</param>
	public static void AddConverter<T>(IConverterDefinition<T> converter)
	{
		lock (ConverterDefinitions)
			ConverterDefinitions[converter.ClrType] = converter;
	}

	/// <summary>
	/// Registers a custom converter for type <typeparamref name="T"/> using inline delegates.
	/// </summary>
	/// <typeparam name="T">The CLR type to convert.</typeparam>
	/// <param name="getter">Delegate that reads a value from a statement column.</param>
	/// <param name="setter">Delegate that binds a value to a statement parameter.</param>
	/// <param name="cellType">Delegate that determines the SQLite cell type for columns of this converter.</param>
	public static void AddConverter<T>(StatementGetterFunc<T> getter, StatementSetterFunc<T> setter, DetermineCellTypeFunc cellType)
	{
		var actualType = (typeof(T).IsValueType ? typeof(StructConverterDefinition<>) : typeof(ConverterDefinition<>)).MakeGenericType(typeof(T));

		var converter = (IGenericConverterDefinition)Activator.CreateInstance(actualType, getter, setter, cellType);

		lock (ConverterDefinitions)
		{
			ConverterDefinitions[typeof(T)] = converter;
		}
	}

	static ValueConverter()
	{
		AddConverter(new ConverterDefinition<string>
		(
			static (statement, index, column, colType) => SQLite3Native.ColumnString(statement, index),
			static (statement, index, column, value) => SQLite3Native.BindText(statement, index, value, -1, -1),
			static _ => SqliteCellType.Text
		));

		AddConverter(new StructConverterDefinition<int>
		(
			static (statement, index, column, colType) => SQLite3Native.ColumnInt(statement, index),
			static (statement, index, column, value) => SQLite3Native.BindInt(statement, index, value),
			static _ => SqliteCellType.Integer
		));

		AddConverter(new StructConverterDefinition<bool>
		(
			static (statement, index, column, colType) => SQLite3Native.ColumnInt(statement, index) == 1,
			static (statement, index, column, value) => SQLite3Native.BindInt(statement, index, value ? 1 : 0),
			static _ => SqliteCellType.Integer
		));

		AddConverter(new StructConverterDefinition<double>
		(
			static (statement, index, column, colType) => SQLite3Native.ColumnDouble(statement, index),
			static (statement, index, column, value) => SQLite3Native.BindDouble(statement, index, value),
			static _ => SqliteCellType.Real
		));

		AddConverter(new StructConverterDefinition<float>
		(
			static (statement, index, column, colType) => (float)SQLite3Native.ColumnDouble(statement, index),
			static (statement, index, column, value) => SQLite3Native.BindDouble(statement, index, value),
			static _ => SqliteCellType.Real
		));

		AddConverter(new StructConverterDefinition<long>
		(
			static (statement, index, column, colType) => SQLite3Native.ColumnInt64(statement, index),
			static (statement, index, column, value) => SQLite3Native.BindInt64(statement, index, value),
			static _ => SqliteCellType.Integer
		));

		AddConverter(new StructConverterDefinition<ulong>
		(
			static (statement, index, column, colType) => (ulong)SQLite3Native.ColumnInt64(statement, index),
			static (statement, index, column, value) => SQLite3Native.BindInt64(statement, index, (long)value),
			static _ => SqliteCellType.Integer
		));

		AddConverter(new StructConverterDefinition<uint>
		(
			static (statement, index, column, colType) => (uint)SQLite3Native.ColumnInt64(statement, index),
			static (statement, index, column, value) => SQLite3Native.BindInt64(statement, index, (long)value),
			static _ => SqliteCellType.Integer
		));

		AddConverter(new StructConverterDefinition<decimal>
		(
			static (statement, index, column, colType) =>
			{
				return colType switch
				{
					SQLite3Native.ColType.Integer => (decimal)SQLite3Native.ColumnInt64(statement, index),
					SQLite3Native.ColType.Real => (decimal)SQLite3Native.ColumnDouble(statement, index),
					_ => decimal.Parse(SQLite3Native.ColumnString(statement, index))
				};
			},
			static (statement, index, column, value) => SQLite3Native.BindText(statement, index, value.ToString(CultureInfo.InvariantCulture), -1, -1),
			static _ => SqliteCellType.Text
		));

		AddConverter(new StructConverterDefinition<byte>
		(
			static (statement, index, column, colType) => (byte)SQLite3Native.ColumnInt(statement, index),
			static (statement, index, column, value) => SQLite3Native.BindInt(statement, index, value),
			static _ => SqliteCellType.Integer
		));

		AddConverter(new StructConverterDefinition<ushort>
		(
			static (statement, index, column, colType) => (ushort)SQLite3Native.ColumnInt(statement, index),
			static (statement, index, column, value) => SQLite3Native.BindInt(statement, index, value),
			static _ => SqliteCellType.Integer
		));

		AddConverter(new StructConverterDefinition<short>
		(
			static (statement, index, column, colType) => (short)SQLite3Native.ColumnInt(statement, index),
			static (statement, index, column, value) => SQLite3Native.BindInt(statement, index, value),
			static _ => SqliteCellType.Integer
		));

		AddConverter(new StructConverterDefinition<sbyte>
		(
			static (statement, index, column, colType) => (sbyte)SQLite3Native.ColumnInt(statement, index),
			static (statement, index, column, value) => SQLite3Native.BindInt(statement, index, value),
			static _ => SqliteCellType.Integer
		));

		AddConverter(new ConverterDefinition<byte[]>
		(
static (statement, index, column, colType) => SQLite3Native.ColumnByteArray(statement, index),
static (statement, index, column, value) => SQLite3Native.BindBlob(statement, index, value, value.Length, -1),
static _ => SqliteCellType.Blob
));

		AddConverter(new StructConverterDefinition<Guid>
		(
			static (statement, index, column, colType) => new Guid(SQLite3Native.ColumnString(statement, index)),
			static (statement, index, column, value) => SQLite3Native.BindText(statement, index, value.ToString(), -1, -1),
			static _ => SqliteCellType.Text
		));

		AddConverter(new StructConverterDefinition<TimeSpan>
		(
			static (statement, index, column, colType) =>
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
			static (statement, index, column, value) =>
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
			static column => column?.StoreAsText == true ? SqliteCellType.Text : SqliteCellType.Integer
		));

		AddConverter(new StructConverterDefinition<DateTime>
		(
			static (statement, index, column, colType) =>
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
			static (statement, index, column, value) =>
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
			static column => column?.StoreAsText == true ? SqliteCellType.Text : SqliteCellType.Integer
		));

		AddConverter(new StructConverterDefinition<DateTimeOffset>
		(
			static (statement, index, column, colType) =>
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
			static (statement, index, column, value) =>
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
			static column => column?.StoreAsText == true ? SqliteCellType.Text : SqliteCellType.Integer
		));

		AddConverter(new StructConverterDefinition<DateOnly>
		(
			static (statement, index, column, colType) =>
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
			static (statement, index, column, value) =>
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
			static column => column?.StoreAsText == true ? SqliteCellType.Text : SqliteCellType.Integer
		));

		AddConverter(new StructConverterDefinition<TimeOnly>
		(
			static (statement, index, column, colType) =>
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
			static (statement, index, column, value) =>
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
			static column => column?.StoreAsText == true ? SqliteCellType.Text : SqliteCellType.Integer
		));

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

	private static class ILHandler
	{
		public static readonly Dictionary<FieldInfo, object> ILSetterCache = new();

		public static ActionRef<TTarget, TValue> CreateILSetter<TTarget, TValue>(FieldInfo field)
		{
			if (ILSetterCache.TryGetValue(field, out var existing))
				return (ActionRef<TTarget, TValue>)existing;

			if (field == null) throw new ArgumentNullException(nameof(field));
			if (field.IsInitOnly) throw new ArgumentException("Field is readonly.", nameof(field));
			if (field.IsStatic) throw new ArgumentException("Field must be an instance field.", nameof(field));
			if (field.DeclaringType != typeof(TTarget))
				throw new ArgumentException("TTarget must match field.DeclaringType.", nameof(field));

			var methodName = "fourlambda_sqlite_" + Guid.NewGuid().ToString("N");

			var dm = new DynamicMethod(
				methodName,
				typeof(void),
				[typeof(TTarget).MakeByRefType(), typeof(TValue)],
				restrictedSkipVisibility: true);

			var il = dm.GetILGenerator();

			// Push target reference/address:
			il.Emit(OpCodes.Ldarg_0); // stack: &TTarget

			if (!field.DeclaringType.IsValueType)
			{
				// For reference-type declaring types, Ldarg_0 is a managed pointer to the reference.
				// We need the reference itself on the stack for stfld -> load it via ldind.ref
				il.Emit(OpCodes.Ldind_Ref); // stack: objectRef
			}
			// For value types, the managed pointer (&TTarget) is exactly what stfld expects.

			// Push value (TValue)
			il.Emit(OpCodes.Ldarg_1); // stack: targetRef / &target, value

			// If the field type is Nullable<TValue> and TValue is the underlying value type,
			// construct Nullable<TValue> from the TValue on the stack.
			var fieldType = field.FieldType;
			if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				var nullableCtor = fieldType.GetConstructor([typeof(TValue)]);
				il.Emit(OpCodes.Newobj, nullableCtor); // replaces value on stack with Nullable<TValue>
			}

			// Emit stfld (instance field)
			il.Emit(OpCodes.Stfld, field);

			il.Emit(OpCodes.Ret);

			var @delegate = (ActionRef<TTarget, TValue>)dm.CreateDelegate(typeof(ActionRef<TTarget, TValue>));
			ILSetterCache[field] = @delegate;
			return @delegate;
		}
	}
}