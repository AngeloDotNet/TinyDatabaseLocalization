namespace TinyLocalization.Enums;

/// <summary>
/// Specifies the types of databases supported by the application.
/// </summary>
/// <remarks>Use this enumeration to indicate which database provider is in use. The None value indicates that no
/// database type is selected. Supported options include SQLite, SQL Server, PostgreSQL, and MySQL. Selecting the
/// appropriate value ensures correct configuration and behavior when interacting with the underlying data
/// store.</remarks>
public enum DatabaseType
{
    None = 0,
    SQLite,
    SqlServer,
    PostgreSQL,
    MySQL
    //Oracle,
    //InMemory
}
