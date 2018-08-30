#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (FileList.cs) is part of Oetools.Utilities.
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Oetools.Utilities.Lib {

    /// <summary>
    /// Class to handle a list of unique files
    /// The point of this implementation is to quickly find out if a file exists in this list
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FileList<T> : IEnumerable<T> where T : IFileListItem {
        
        private Dictionary<string, T> _dic = new Dictionary<string, T>(Utils.IsRuntimeWindowsPlatform ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
               
        public T this[string filePath] {
            get => !string.IsNullOrEmpty(filePath) && _dic.ContainsKey(filePath) ? _dic[filePath] : default(T);
            set {
                if (filePath == null) {
                    throw new ArgumentNullException(nameof(filePath));
                }
                if (_dic.ContainsKey(filePath)) {
                    _dic[filePath] = value;
                } else {
                    _dic.Add(filePath, value);
                }
            }
        }
        
        public T this[T file] {
            get => this[file?.FilePath];
            set => this[file?.FilePath] = value;
        }
        
        public IEnumerator<T> GetEnumerator() {
            return _dic.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(T item) {
            if (item?.FilePath == null) {
                throw new ArgumentNullException(nameof(item));
            }
            _dic.Add(item.FilePath, item);
        }

        public void AddRange(IEnumerable<T> list) {
            if (list != null) {
                foreach (var item in list.Where(item => item?.FilePath != null)) {
                    _dic.Add(item.FilePath, item);
                }
            }
        }

        public int TryAddRange(IEnumerable<T> list) {
            int nbAdded = 0;
            if (list != null) {
                foreach (var item in list.Where(item => item?.FilePath != null && !_dic.ContainsKey(item.FilePath))) {
                    _dic.Add(item.FilePath, item);
                    nbAdded++;
                }
            }
            return nbAdded;
        }

        public bool TryAdd(T item) {
            if (item?.FilePath == null) {
                throw new ArgumentNullException(nameof(item));
            }
            if (!_dic.ContainsKey(item.FilePath)) {
                _dic.Add(item.FilePath, item);
                return true;
            }
            return false;
        }

        public void Clear() {
            _dic.Clear();
        }

        public bool Contains(T item) {
            return Contains(item?.FilePath);
        }

        public bool Contains(string path) {
            return !string.IsNullOrEmpty(path) && _dic.ContainsKey(path);
        }

        public bool Remove(T item) {
            if (item == null) {
                throw new ArgumentNullException(nameof(item));
            }
            return _dic.Remove(item.FilePath);
        }

        public int Count => _dic.Count;
        
        public FileList<TResult> Select<TResult>(Func<T, TResult> selector) where TResult : IFileListItem {
            var output = new FileList<TResult>();
            foreach (var item in this) {
                output.Add(selector(item));
            }
            return output;
        }
        
        /// <summary>
        /// Returns of a copy of this list, with only the items selected
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        public FileList<T> CopyWhere(Func<T, bool> selector) {
            var output = new FileList<T>();
            foreach (var item in this) {
                if (selector(item)) {
                    output.Add(item);
                }
            }
            return output;
        }
    }
}