/*
    This file is part of MqttSql (Copyright © 2024  Guiorgy).
    MqttSql is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
    MqttSql is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License along with MqttSql. If not, see <https://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;
using System.Linq;

namespace MqttSql
{
    public static class Extensions
    {
        //
        // Summary:
        //     Determines whether a sequence contains any of the specified elements
        //     by using the default equality comparer.
        //
        // Parameters:
        //   source:
        //     A sequence in which to locate a value.
        //
        //   values:
        //     The values to locate in the sequence.
        //
        // Type parameters:
        //   TSource:
        //     The type of the elements of source.
        //
        // Returns:
        //     true if the source sequence contains an element that has any of the
        //     specified values; otherwise, false.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     source or values is null.
        public static bool ContainsAny<TSource>(this IEnumerable<TSource> source, IEnumerable<TSource> values)
        {
            return source.Intersect(values).Any();
        }
        public static bool ContainsAny<TSource>(this IEnumerable<TSource> source, params TSource[] values)
        {
            return source.Intersect(values).Any();
        }

        //
        // Summary:
        //     Determines whether a sequence contains all of the specified elements
        //     by using the default equality comparer.
        //
        // Parameters:
        //   source:
        //     A sequence in which to locate a value.
        //
        //   values:
        //     The values to locate in the sequence.
        //
        // Type parameters:
        //   TSource:
        //     The type of the elements of source.
        //
        // Returns:
        //     true if the source sequence contains elements that have the specified
        //     values; otherwise, false.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     source or values is null.
        public static bool ContainsAal<TSource>(this IEnumerable<TSource> source, IEnumerable<TSource> values)
        {
            return source.Intersect(values).Count() == values.Count();
        }
        public static bool ContainsAal<TSource>(this IEnumerable<TSource> source, params TSource[] values)
        {
            return source.Intersect(values).Count() == values.Count();
        }
    }
}
