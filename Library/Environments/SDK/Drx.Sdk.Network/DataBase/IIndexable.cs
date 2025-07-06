namespace Drx.Sdk.Network.DataBase
{
    /// <summary>
    /// Defines a contract for objects that can be stored in an IndexedRepository.
    /// It ensures that every stored item has a unique string identifier.
    /// </summary>
    public interface IIndexable
    {
        /// <summary>
        /// Gets the unique identifier for the object.
        /// </summary>
        string Id { get; }
    }
} 