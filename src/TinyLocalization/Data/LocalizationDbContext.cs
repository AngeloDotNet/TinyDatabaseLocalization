using Microsoft.EntityFrameworkCore;
using TinyLocalization.Entities;

namespace TinyLocalization.Data;

/// <summary>
/// Represents the Entity Framework database context for managing localization data, including translations.
/// </summary>
/// <remarks>Use this context to interact with the localization database, providing access to translation entities
/// and enabling querying, addition, modification, and deletion of localized content.</remarks>
/// <param name="options">The options to configure the context, such as database connection settings and other context-specific options.</param>
public class LocalizationDbContext(DbContextOptions<LocalizationDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets or sets the collection of translations stored in the database.
    /// </summary>
    /// <remarks>This property provides access to all translation entities, enabling querying, addition,
    /// modification, and deletion of localized content within the database context.</remarks>
    public virtual DbSet<Translation> Translations { get; set; }

    /// <summary>
    /// Configures the entity model for the database context using the specified model builder.
    /// </summary>
    /// <remarks>This method is called by Entity Framework during model creation to allow customization of
    /// entity mappings, table names, keys, and indexes. Override this method to configure the model as needed before
    /// the context is used.</remarks>
    /// <param name="modelBuilder">The builder used to construct the model for the context. Provides configuration for entity types, relationships,
    /// and database schema.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Translation>(entity =>
        {
            entity.ToTable("Translations");
            entity.HasKey(t => t.Id);

            entity.HasIndex(t => new { t.Resource, t.Key, t.Culture }).IsUnique();
        });
    }
}