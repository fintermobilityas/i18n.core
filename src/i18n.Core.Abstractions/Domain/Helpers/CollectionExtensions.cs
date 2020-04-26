using System.Collections.Generic;
using System.Linq;

namespace i18n.Core.Abstractions.Domain.Helpers
{
    internal static class CollectionExtensions
    {
        /// <summary>
        /// Returns a copy of a collection with the contents of another collection appended to it.
        /// </summary>
        public static IEnumerable<T> Append<T>(this IEnumerable<T> lhs, IEnumerable<T> rhs)
        {
            var list = lhs.ToList();
            list.AddRange(rhs);
            return list;
        }

        /// <summary>
        /// Returns a copy of a collection with a new single item added to it.
        /// </summary>
        public static IEnumerable<T> Append<T>(this IEnumerable<T> lhs, T rhs)
        {
            var list = lhs.ToList();
            list.Add(rhs);
            return list;
        }
    }
}
