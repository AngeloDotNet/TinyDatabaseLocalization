using Microsoft.EntityFrameworkCore;
using TinyLocalization.Entities;

namespace TinyLocalization.Data;

/// <summary>
/// Represents the Entity Framework Core database context for TinyLocalization.
/// Contains the <see cref="Translations"/> DbSet and configures entity mappings.
/// </summary>
/// <param name="options">The options to configure the context, provided to the base <see cref="DbContext"/>.</param>
public class LocalizationDbContext(DbContextOptions<LocalizationDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets or sets the translations stored in the database.
    /// Each <see cref="Translation"/> represents a localized string for a specific resource, key and culture.
    /// </summary>
    public virtual DbSet<Translation> Translations { get; set; }

    /// <summary>
    /// Configures the EF Core model for the localization entities.
    /// This method configures table names, keys and unique indexes for the <see cref="Translation"/> entity.
    /// </summary>
    /// <param name="modelBuilder">The <see cref="ModelBuilder"/> used to configure entity mappings and relationships.</param>
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