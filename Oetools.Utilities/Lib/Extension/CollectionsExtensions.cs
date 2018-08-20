#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (CollectionsExtensions.cs) is part of Oetools.Utilities.
// 
// Oetools.Utilities is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Oetools.Utilities is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion
using System.Collections.Generic;
using System.Linq;

namespace Oetools.Utilities.Lib.Extension {
    public static class CollectionsExtensions {
        /// <summary>
        /// Converts an IEnumerable to a HashSet, optionally add to an existing hashset
        /// </summary>
        /// <typeparam name="T">The IEnumerable type</typeparam>
        /// <param name="enumerable">The IEnumerable</param>
        /// <param name="existingHashSet"></param>
        /// <returns>A new HashSet</returns>
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> enumerable, HashSet<T> existingHashSet = null) {
            if (existingHashSet == null) {
                existingHashSet = new HashSet<T>();
            }

            foreach (T item in enumerable) {
                if (!existingHashSet.Contains(item)) {
                    existingHashSet.Add(item);
                }
            }

            return existingHashSet;
        }

        /// <summary>
        /// Same as Union, expect one or both arguments can be null; will always return an empty list in the worse case
        /// </summary>
        /// <param name="enumerable"></param>
        /// <param name="enumerable2"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static List<T> Union2<T>(this IEnumerable<T> enumerable, IEnumerable<T> enumerable2) {
            var output = new List<T>();
            if (enumerable != null) {
                output.AddRange(enumerable);
            }
            if (enumerable2 != null) {
                output.AddRange(enumerable2);
            }
            return output;
        }
        
        /// <summary>
        ///     Same as ToList but returns an empty list on Null instead of an exception
        /// </summary>
        public static List<T> ToNonNullList<T>(this IEnumerable<T> obj) {
            return obj == null ? new List<T>() : obj.ToList();
        }
    }
}