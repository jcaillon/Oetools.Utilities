#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (EnumExtensions.cs) is part of Oetools.Utilities.
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
using System;
using System.Collections.Generic;

namespace Oetools.Utilities.Lib.Extension {

    /// <summary>
    /// This class regroups all the extension methods
    /// </summary>
    public static class EnumExtensions {
        
        // TODO : replace type by string?
        private static Dictionary<Type, List<Tuple<string, long>>> _enumTypeNameValueKeyPairs = new Dictionary<Type, List<Tuple<string, long>>>();

        public static void ForEach<T>(this Type curType, Action<string, long> actionForEachNameValue) {
            if (!curType.IsEnum)
                return;
            if (!_enumTypeNameValueKeyPairs.ContainsKey(curType)) {
                var list = new List<Tuple<string, long>>();
                foreach (var name in Enum.GetNames(curType)) {
                    var val = (T) Enum.Parse(curType, name);
                    list.Add(new Tuple<string, long>(name, Convert.ToInt64(val)));
                }
                _enumTypeNameValueKeyPairs.Add(curType, list);
            }

            foreach (var tuple in _enumTypeNameValueKeyPairs[curType]) {
                actionForEachNameValue(tuple.Item1, tuple.Item2);
            }
        }
      
        /// <summary>
        /// MyEnum tester = MyEnum.FlagA | MyEnum.FlagB;
        /// if(tester.IsSet(MyEnum.FlagA))
        /// </summary>
        public static bool IsFlagSet(this Enum input, Enum matchTo) {
            return (Convert.ToUInt32(input) & Convert.ToUInt32(matchTo)) != 0;
        }

    }
}