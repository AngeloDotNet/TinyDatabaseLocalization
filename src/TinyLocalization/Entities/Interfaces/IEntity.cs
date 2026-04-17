namespace TinyLocalization.Entities.Interfaces;

/// <summary>
/// Defines a contract for entities with a strongly-typed identifier.
/// </summary>
/// <remarks>This interface is typically used to provide a consistent way to access and set the unique identifier
/// for entities in a domain model. Implementing this interface allows generic handling of entities regardless of their
/// identifier type.</remarks>
/// <typeparam name="T">The type of the entity identifier.</typeparam>
public interface IEntity<T>
{
    T Id { get; set; }
}