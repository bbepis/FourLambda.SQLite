namespace FourLambda.SQLite;

[AttributeUsage(AttributeTargets.Class)]
public class TableAttribute(string name) : Attribute
{
	public string Name { get; set; } = name;

	/// <summary>
	/// Flag whether to create the table without rowid (see https://sqlite.org/withoutrowid.html)
	///
	 /// The default is <c>false</c> so that sqlite adds an implicit <c>rowid</c> to every table created.
	/// </summary>
	public bool WithoutRowId { get; set; }

	public bool Strict { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public class ColumnAttribute(string name) : Attribute
{
	public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Property)]
public class PrimaryKeyAttribute(int order = 0) : Attribute
{
	public int Order { get; init; } = order;
}

[AttributeUsage(AttributeTargets.Property)]
public class AutoIncrementAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class IndexedAttribute : Attribute
{
	public string? Name { get; set; }
	public int Order { get; set; }
	public virtual bool Unique { get; init; }

	public IndexedAttribute() { }

	public IndexedAttribute(string name, int order)
	{
		Name = name;
		Order = order;
	}
}

[AttributeUsage(AttributeTargets.Property)]
public class IgnoreAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public class UniqueAttribute : IndexedAttribute
{
	public override bool Unique => true;
}

[AttributeUsage(AttributeTargets.Property)]
public class MaxLengthAttribute(int length) : Attribute
{
	public int Value { get; } = length;
}

/// <summary>
/// Select the collating sequence to use on a column.
/// "BINARY", "NOCASE", and "RTRIM" are supported.
/// "BINARY" is the default.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class CollationAttribute(string collation) : Attribute
{
	public string Collation { get; } = collation;
}

[AttributeUsage(AttributeTargets.Property)]
public class NotNullAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public class StoreAsTextAttribute(string? format = null) : Attribute
{
	/// <summary>
	/// The format argument to pass into <see cref="DateTime.ToString(string)"/> or equivalent methods.
	/// </summary>
	public string? Format { get; set; } = format;
}

/// <summary>
/// Immutable mapping from a database table to a .NET type (or a manual column definition).
/// Construct via <see cref="TableMappingBuilder"/>.
/// </summary>
public sealed class TableMapping
{
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
	public Type? MappedType { get; }

	public string TableName { get; }
	public bool WithoutRowId { get; }
	public bool Strict { get; }
	public TableColumn[] Columns { get; }
	public TableColumn[] PrimaryKeyColumns { get; }
	internal string? PKWhereSql { get; }
	public TableCreateFlags CreateFlags { get; }
	public bool HasAutoIncPK { get; }

	TableColumn? _autoPk;

	public TableColumn[] InsertColumns { get; }
	public TableColumn[] InsertOrReplaceColumns { get; }

internal TableMapping(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type? mappedType,
		string tableName,
		bool withoutRowId,
		bool strict,
		TableColumn[] columns,
		TableCreateFlags createFlags)
	{
		MappedType = mappedType;
		TableName = tableName;
		WithoutRowId = withoutRowId;
		Strict = strict;
		Columns = columns;
		CreateFlags = createFlags;

		PrimaryKeyColumns = Columns.Where(c => c.IsPK).OrderBy(c => c.PKOrder).ToArray();

		if (PrimaryKeyColumns.Length > 1)
		{
			if (PrimaryKeyColumns.Any(c => c.IsAutoInc))
				throw new ArgumentException("Table with composite primary key cannot have auto incrementing");

			if (PrimaryKeyColumns.Count(c => c.PKOrder == 0) > 1)
				throw new ArgumentException("Table with composite primary key must have explicit ordering of individual primary keys");
		}
		else
		{
			_autoPk = PrimaryKeyColumns.FirstOrDefault(x => x.IsAutoInc);
		}

		HasAutoIncPK = _autoPk != null;

		if (PrimaryKeyColumns.Length > 0)
		{
			PKWhereSql = "where " + string.Join(" and ", PrimaryKeyColumns.Select(pk => $"\"{pk.Name}\" = ?"));
		}
		else
		{
			PKWhereSql = null;
		}

		InsertColumns = Columns.Where(c => !c.IsAutoInc).ToArray();
		InsertOrReplaceColumns = Columns.ToArray();
	}

	internal void SetAutoIncPK(object obj, long id)
	{
		if (_autoPk != null)
		{
			_autoPk.SetValue(obj, Convert.ChangeType(id, _autoPk.ColumnType, null));
		}
	}

	internal TableColumn? FindColumnWithPropertyName(string propertyName)
	{
		return Columns.FirstOrDefault(c => c.PropertyName == propertyName);
	}

	internal TableColumn? FindColumn(string columnName)
	{
		return Columns.FirstOrDefault(c => string.Equals(c.Name, columnName, StringComparison.OrdinalIgnoreCase));
	}
}

/// <summary>
/// Immutable column within a <see cref="TableMapping"/>. Construct via <see cref="TableMappingBuilder"/>.
/// </summary>
public sealed class TableColumn
{
	public string Name { get; }
	public PropertyInfo? PropertyInfo { get; }
	public string? PropertyName => PropertyInfo?.Name;
	public Type ColumnType { get; }
	public SqliteCellType SqliteType { get; }
	public string? Collation { get; }
	public bool IsAutoInc { get; }
	public bool IsAutoGuid { get; }
	public bool IsPK => PKOrder.HasValue;
	public int? PKOrder { get; }
	public IEnumerable<IndexedAttribute> Indices { get; }
	public bool IsNullable { get; }
	public int? MaxStringLength { get; }
	public bool StoreAsText { get; }
	public string? StoreAsTextFormat { get; }

	internal TableColumn(
		string name,
		PropertyInfo? propertyInfo,
		Type columnType,
		SqliteCellType sqliteType,
		string? collation,
		bool isAutoInc,
		bool isAutoGuid,
		int? pkOrder,
		IEnumerable<IndexedAttribute> indices,
		bool isNullable,
		int? maxStringLength,
		bool storeAsText,
		string? storeAsTextFormat)
	{
		Name = name;
		PropertyInfo = propertyInfo;
		ColumnType = columnType;
		SqliteType = sqliteType;
		Collation = collation;
		IsAutoInc = isAutoInc;
		IsAutoGuid = isAutoGuid;
		PKOrder = pkOrder;
		Indices = indices;
		IsNullable = isNullable;
		MaxStringLength = maxStringLength;
		StoreAsText = storeAsText;
		StoreAsTextFormat = storeAsTextFormat;
	}

	public void SetValue(object obj, object? val)
	{
		if (val != null && ColumnType.GetTypeInfo().IsEnum)
			PropertyInfo!.SetValue(obj, Enum.ToObject(ColumnType, val));
		else
			PropertyInfo!.SetValue(obj, val);
	}

	public object? GetValue(object obj)
	{
		return PropertyInfo!.GetValue(obj);
	}

	public ValueConverter.IGenericConverterDefinition GetConverter()
	{
		if (!ValueConverter.TryGetConverterDefinition(ColumnType, out var definition))
			throw new NotSupportedException("Unable to convert type " + ColumnType);

		return definition;
	}

	internal string GetCreationSql()
	{
		var sqliteType = SqliteType switch
		{
			SqliteCellType.Integer => "INTEGER",
			SqliteCellType.Real => "REAL",
			SqliteCellType.Text => "TEXT",
			SqliteCellType.Blob => "BLOB",
			SqliteCellType.Any => "ANY",
			_ => throw new ArgumentOutOfRangeException()
		};

		string decl = $"\"{Name}\" {sqliteType}";

		if (!IsNullable)
		{
			decl += " not null";

			if (!IsPK)
			{
				// TODO: set default value for other types
				if (SqliteType == SqliteCellType.Integer)
					decl += " default 0";
			}
		}

		if (!string.IsNullOrEmpty(Collation))
		{
			decl += $" collate {Collation}";
		}

		return decl;
	}
}

public enum SqliteCellType
{
	Integer,
	Real,
	Text,
	Blob,
	Any
}

/// <summary>
/// Builder for creating immutable <see cref="TableMapping"/> instances.
/// Supports type-backed initialization with optional column customization,
/// and manual table definition without a concrete .NET type.
/// </summary>
public class TableMappingBuilder
{
	private Type? _baseType = null;

	public TableCreateFlags CreateFlags { get; set; }
	public bool WithoutRowId { get; set; }
	public bool Strict { get; set; }
	
	public List<ColumnDefinition> Columns { get; internal init; } = new();

	/// <summary>
	/// Start building a mapping from a concrete .NET type using reflection to discover columns.
	/// </summary>
	public static TableMappingBuilder FromType<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
		T>(
		TableCreateFlags createFlags = TableCreateFlags.None,
		Action<ColumnDefinition>? configure = null)
	{
		return FromType(typeof(T), createFlags, configure);
	}


	/// <summary>
	/// Start building a mapping from a concrete .NET type using reflection to discover columns.
	/// </summary>
	public static TableMappingBuilder FromType(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
		TableCreateFlags createFlags = TableCreateFlags.None,
		Action<ColumnDefinition>? configure = null)
	{
		var tableAttr = type.GetCustomAttribute<TableAttribute>();

		var members = GetPublicMembers(type);
		var cols = new List<ColumnDefinition>(members.Count);

		foreach (var m in members)
		{
			if (m.IsDefined(typeof(IgnoreAttribute), true))
				continue;

			var colDef = new ColumnDefinition(m, createFlags);
			configure?.Invoke(colDef);
			cols.Add(colDef);
		}

		bool withoutRowId = tableAttr?.WithoutRowId ?? false;
		bool strict = tableAttr?.Strict ?? false;

		var builder = new TableMappingBuilder
		{
			_tableName = tableAttr?.Name ?? type.Name,
			WithoutRowId = withoutRowId,
			Strict = strict,
			CreateFlags = createFlags,
			Columns = cols,
			_baseType = type
		};

		return builder;
	}

	static TableMappingBuilder CreateBuilder(
		string tableName,
		ColumnDefinition[] columns,
		bool withoutRowId = false,
		bool strict = false,
		TableCreateFlags flags = TableCreateFlags.None)
	{
		return new TableMappingBuilder
		{
			_tableName = tableName,
			WithoutRowId = withoutRowId,
			Strict = strict,
			CreateFlags = flags,
			Columns = columns.ToList()
		};
	}

	static IReadOnlyCollection<PropertyInfo> GetPublicMembers(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
	{
		var members = new List<PropertyInfo>();
		var memberNames = new HashSet<string>();
		var newMembers = new List<PropertyInfo>();

		do
		{
			var ti = type.GetTypeInfo();
			newMembers.Clear();

			newMembers.AddRange(
				from p in ti.DeclaredProperties
				where !memberNames.Contains(p.Name) &&
				      p.CanRead && p.CanWrite &&
				      p.GetMethod != null && p.SetMethod != null &&
				      p.GetMethod.IsPublic && p.SetMethod.IsPublic &&
				      !p.GetMethod.IsStatic && !p.SetMethod.IsStatic
				select p);

			members.AddRange(newMembers);
			foreach (var m in newMembers)
				memberNames.Add(m.Name);

			type = ti.BaseType;
		}
		while (type != null && type != typeof(object));

		return members;
	}

	/// <summary>
	/// Set the table name. Defaults to <c>MappedType.Name</c> when building from a type.
	/// </summary>
	public TableMappingBuilder TableName(string tableName)
	{
		_tableName = tableName;
		return this;
	}

	string? _tableName;

	/// <summary>
	/// Enable <c>WITHOUT ROWID</c> table storage.
	/// </summary>
	public TableMappingBuilder SetWithoutRowId(bool value = true)
	{
		WithoutRowId = value;
		return this;
	}

	/// <summary>
	/// Enable <c>STRICT</c> table type enforcement.
	/// </summary>
	public TableMappingBuilder SetStrict(bool value = true)
	{
		Strict = value;
		return this;
	}

	/// <summary>
	/// Add a column backed by a property from a type. Use this to build mappings manually
	/// while still having reflection-based value getting/setting.
	 /// </summary>
	public TableMappingBuilder AddColumn(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] PropertyInfo propertyInfo,
		TableCreateFlags createFlags = TableCreateFlags.None,
		Action<ColumnDefinition>? configure = null)
	{
		var colDef = new ColumnDefinition(propertyInfo, createFlags);
		configure?.Invoke(colDef);
		return AddColumn(colDef);
	}

	/// <summary>
	/// Add a manually defined column without a backing property.
	/// Use this for ad-hoc query results or tables not backed by a concrete class.
	/// </summary>
	public TableMappingBuilder AddColumn(string name, Type clrType, bool isNullable = true)
	{
		var colDef = new ColumnDefinition(name, clrType)
		{
			IsNullable = isNullable
		};

		return AddColumn(colDef);
	}

	TableMappingBuilder AddColumn(ColumnDefinition colDef)
	{
		Columns.Add(colDef);
		return this;
	}

	/// <summary>
	/// Build the immutable <see cref="TableMapping"/>.
	/// </summary>
	public TableMapping Build()
	{
		_tableName ??= _baseType?.Name;

		if (string.IsNullOrWhiteSpace(_tableName))
			throw new ArgumentException("Mapping must have a valid table name.");

		if (Columns.Count == 0)
			throw new ArgumentException("Mapping must have at least one column.");

		return new TableMapping(
			_baseType,
			_tableName,
			WithoutRowId,
			Strict,
			Columns.Select(x => x.Build()).ToArray(),
			CreateFlags);
	}
}

/// <summary>
/// Mutable column definition used during building. Immutable after <see cref="Build"/> is called on the parent builder.
/// </summary>
public class ColumnDefinition
{
	public string Name { get; set; }
	public PropertyInfo? PropertyInfo { get; private set; }
	public Type ColumnType { get; set; }
	public SqliteCellType? SqliteType { get; set; }
	public string? Collation { get; set; }
	public bool IsAutoGuid { get; set; }
	public int? PrimaryKeyPosition { get; set; }

	public bool IsAutoIncrement { get; set; }

	public IEnumerable<IndexedAttribute> Indices { get; set; } = Array.Empty<IndexedAttribute>();
	public bool IsNullable { get; set; }
	public int? MaxStringLength { get; set; }
	public bool StoreAsText { get; set; }
	public string? StoreAsTextFormat { get; set; }

	internal ColumnDefinition(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] PropertyInfo propertyInfo,
		TableCreateFlags createFlags = TableCreateFlags.None)
	{
		PropertyInfo = propertyInfo;
		var memberType = propertyInfo.PropertyType;

		var colAttr = propertyInfo.GetCustomAttribute<ColumnAttribute>();
		Name = colAttr?.Name ?? propertyInfo.Name;

		ColumnType = Nullable.GetUnderlyingType(memberType) ?? memberType;
		Collation = propertyInfo.GetCustomAttribute<CollationAttribute>()?.Collation;

		var memberStoreAsTextAttr = propertyInfo.GetCustomAttribute<StoreAsTextAttribute>();
		StoreAsText = memberStoreAsTextAttr != null || memberType.GetCustomAttribute<StoreAsTextAttribute>() != null;
		StoreAsTextFormat = memberStoreAsTextAttr?.Format;

		PrimaryKeyPosition = propertyInfo.GetCustomAttribute<PrimaryKeyAttribute>()?.Order;

		var implicitPk = createFlags.HasFlag(TableCreateFlags.ImplicitPK)
		                 && string.Equals(propertyInfo.Name, Orm.ImplicitPkName, StringComparison.OrdinalIgnoreCase);

		if (!PrimaryKeyPosition.HasValue && implicitPk)
			PrimaryKeyPosition = 0;

		bool isPk = PrimaryKeyPosition.HasValue;

		var isAutoIncrement = propertyInfo.GetCustomAttribute<AutoIncrementAttribute>() != null
		             || (isPk && createFlags.HasFlag(TableCreateFlags.AutoIncPK));

		IsAutoGuid = isAutoIncrement && ColumnType == typeof(Guid);
		IsAutoIncrement = isAutoIncrement && !IsAutoGuid;

		Indices = propertyInfo.GetCustomAttributes<IndexedAttribute>().ToArray();

		bool markedNotNull = propertyInfo.GetCustomAttribute<NotNullAttribute>() != null;

		var nullabilityInfo = new NullabilityInfoContext().Create(propertyInfo);
		bool explicitlyNull = Nullable.GetUnderlyingType(memberType) != null || nullabilityInfo.WriteState == NullabilityState.Nullable;

		if (explicitlyNull && isPk)
			throw new ArgumentException("A column marked as primary key cannot be nullable.");

		if (explicitlyNull && markedNotNull)
			throw new ArgumentException("A column cannot be marked as [NotNull] while having an explicitly nullable property type.");

		IsNullable = explicitlyNull || ((createFlags.HasFlag(TableCreateFlags.ImplicitNullable) || nullabilityInfo.WriteState == NullabilityState.Unknown) && !(isPk || markedNotNull));
		
		MaxStringLength = propertyInfo.GetCustomAttribute<MaxLengthAttribute>()?.Value;
	}

	internal ColumnDefinition(
		string name,
		Type clrType)
	{
		Name = name;
		ColumnType = clrType;
	}

	public ColumnDefinition WithName(string name)
	{
		Name = name;
		return this;
	}

	public ColumnDefinition WithPrimaryKey(int position = 0)
	{
		PrimaryKeyPosition = position;
		return this;
	}

	public ColumnDefinition WithAutoIncrement(bool value = true)
	{
		IsAutoIncrement = value && !IsAutoGuid;
		IsAutoGuid = value && ColumnType == typeof(Guid);
		return this;
	}

	public ColumnDefinition WithCollation(string collation)
	{
		Collation = collation;
		return this;
	}

	public ColumnDefinition WithStoreAsText(bool value = true, string? format = null)
	{
		StoreAsText = value;
		StoreAsTextFormat = format;
		return this;
	}

	public ColumnDefinition WithNullable(bool value = true)
	{
		IsNullable = value;
		return this; }

	internal TableColumn Build()
	{
		return new TableColumn(
			Name,
			PropertyInfo,
			ColumnType,
			SqliteType ?? ComputeSqlCellType(),
			Collation,
			IsAutoIncrement,
			IsAutoGuid,
			PrimaryKeyPosition,
			Indices,
			IsNullable,
			MaxStringLength,
			StoreAsText,
			StoreAsTextFormat);
	}

	internal SqliteCellType ComputeSqlCellType()
	{
		if (!ValueConverter.TryGetConverterDefinition(ColumnType, out var definition))
			throw new NotSupportedException("Unable to handle column type " + ColumnType);

		return definition!.DetermineCellType(this);
	}
}

public static class Orm
{
	public const string ImplicitPkName = "Id";

	public static Type GetType(object? obj)
	{
		if (obj == null)
			return typeof(object);

		if (obj is IReflectableType rt)
			return rt.GetTypeInfo().AsType();

		return obj.GetType();
	}
}
