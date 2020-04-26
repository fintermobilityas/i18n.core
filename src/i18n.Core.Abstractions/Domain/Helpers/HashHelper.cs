using System.Collections.Generic;
using System.Linq;

namespace i18n.Core.Abstractions.Domain.Helpers
{
    /// <summary>
    /// Helpers for implementing Object.GetHashCode().
    /// http://stackoverflow.com/a/2575444/1173555
    /// </summary>
    internal static class HashHelper
    {
        /// <summary>
        /// Facilitates hashcode generation using fluent interface, like this:
        /// <br />
        ///     return 0.CombineHashCode(field1).CombineHashCode(field2).CombineHashCode(field3);
        /// <br />
        /// </summary>
        /// <param name="hashCode"></param>
        /// <param name="arg">
        /// Subject object, value, or collection (IEnumerable).
        /// </param>
        public static int CombineHashCode<T>(this int hashCode, T arg)
        {
            unchecked // Overflow is fine, just wrap
            {
                return 31 * hashCode + GetHashCode(arg);
                // 31 = prime number.
            }
        }
        /// <summary>
        /// Returns the hash code for the passed argument, with appropriate handling of
        /// null and collection types.
        /// </summary>
        /// <typeparam name="T">Type of subject object.</typeparam>
        /// <param name="arg">Subject object.</param>
        /// <returns>Hash code value.</returns>
        /// <remarks>
        /// For null object, the method simpy returns zero.
        /// For collection objects (castable to IEnumerable&lt;object&gt;) the hash code of
        /// the collection elements are combined to form the result hash code.
        /// </remarks>
        static int GetHashCode<T>(T arg)
        {
            return arg switch
            {
                null => 0,
                IEnumerable<object> collection => collection.Aggregate(0, (current, item) => CombineHashCode<object>(current, item)),
                _ => arg.GetHashCode()
            };
        }
    }
}
