using Microsoft.EntityFrameworkCore;
using TinyLocalization.Enums;

namespace TinyLocalization.Options;

/// <summary>
/// Contains configuration options used to configure the database context for TinyLocalization.
/// </summary>
/// <remarks>
/// These options control how the application connects to and configures the underlying database
/// and Entity Framework Core migrations behavior. Typical usage is to bind configuration values
/// (for example from appsettings.json) to an instance of this type and use it when registering
/// the database context in the dependency injection container.
/// </remarks>
public class DatabaseContextOptions
{
    /// <summary>
    /// Gets or sets the type of database to be used for localization operations.
    /// </summary>
    /// <remarks>The selected database type determines how localization data is stored and accessed. The
    /// default value is DatabaseType.None, which indicates that no database is configured. Changing this property may
    /// affect the behavior of database-related features.</remarks>
    public DatabaseType DatabaseType { get; set; } = DatabaseType.None;

    /// <summary>
    /// Gets or sets the connection string used to establish a connection to the database.
    /// </summary>
    /// <remarks>The connection string must be formatted according to the requirements of the specific
    /// database provider. Ensure that sensitive information, such as passwords, is handled securely and not exposed in
    /// source control or logs.</remarks>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the assembly that contains the migrations for the database context.
    /// </summary>
    /// <remarks>This property is used to specify the assembly where the Entity Framework migrations are
    /// located. It is important to ensure that the specified assembly is accessible and contains the necessary
    /// migration files for the context to function correctly.</remarks>
    public string MigrationsAssembly { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the table used to store Entity Framework migrations history information.
    /// </summary>
    /// <remarks>The default value is "__EFMigrationsHistory". Changing this property allows customization of
    /// the migrations history table name, which can be useful when multiple contexts share a database or when
    /// integrating with existing database naming conventions.</remarks>
    public string MigrationsHistoryTable { get; set; } = "__EFMigrationsHistory";

    /// <summary>
    /// Gets or sets the SQL Server compatibility level used by the database context.
    /// </summary>
    /// <remarks>The default value is 150, which corresponds to SQL Server 2019. Changing this value may
    /// affect the behavior of SQL queries and the availability of certain SQL Server features. Ensure that the
    /// specified compatibility level is supported by your target SQL Server instance.</remarks>
    public int SQLServerCompatibilityLevel { get; set; } = 150;

    /// <summary>
    /// Gets or sets a value indicating whether query tracking behavior is enabled for the context.
    /// </summary>
    /// <remarks>When set to <see langword="true"/>, the context will track changes to entities retrieved from
    /// the database, allowing for automatic updates to the database when changes are made. If set to <see
    /// langword="false"/>, the context will not track changes, which can improve performance in scenarios where
    /// tracking is not needed.</remarks>
    public bool EnableUseQueryTrackingBehavior { get; set; } = false;

    /// <summary>
    /// Gets or sets the behavior used by the context to track changes to entities retrieved from the database.
    /// </summary>
    /// <remarks>Setting this property to <see cref="QueryTrackingBehavior.NoTracking"/> can improve
    /// performance for read-only queries by disabling change tracking. Use <see cref="QueryTrackingBehavior.TrackAll"/>
    /// when you need to detect and persist changes to entities. The selected tracking behavior applies to all queries
    /// executed by the context unless overridden at the query level.</remarks>
    public QueryTrackingBehavior QueryTrackingBehavior { get; set; } = QueryTrackingBehavior.NoTracking;

    /// <summary>
    /// Gets or sets a value indicating whether sensitive data should be included in application logs.
    /// </summary>
    /// <remarks>When enabled, sensitive information such as user credentials or personal data may appear in
    /// logs. Use with caution, especially in production environments, to avoid unintentional exposure of confidential
    /// information.</remarks>
    public bool EnableSensitiveDataLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether detailed error information is enabled.
    /// </summary>
    /// <remarks>When set to <see langword="true"/>, detailed error messages are returned to the client, which
    /// can be useful for debugging. Enabling detailed errors in a production environment may expose sensitive
    /// information and is not recommended.</remarks>
    public bool EnableDetailedErrors { get; set; } = false;
}