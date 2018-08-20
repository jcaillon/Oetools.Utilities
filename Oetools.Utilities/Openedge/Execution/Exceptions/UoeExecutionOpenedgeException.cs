#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionOpenedgeException.cs) is part of Oetools.Utilities.
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
namespace Oetools.Utilities.Openedge.Execution.Exceptions {
    /// <summary>
    /// Happens if there were an openedge runtime exception but we still managed to execute the process
    /// </summary>
    public class UoeExecutionOpenedgeException : UoeExecutionException {
        public int ErrorNumber { get; set; }
        public string ErrorMessage { get; set; }
        public override string Message => $"({ErrorNumber}) {ErrorMessage}";

        /// <summary>
        /// Get an exception from a formatted string "error (nb)",
        /// returns null if the format is incorrect
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static UoeExecutionOpenedgeException GetFromString(string input) {
            var idx = input.LastIndexOf('(');
            if (input.Length == 0 || 
                input[input.Length - 1] != ')' || 
                idx < 0 ||  
                input.Length - 1 - idx - 1 <= 0 || 
                !int.TryParse(input.Substring(idx + 1, input.Length - 1 - idx - 1), out int nb)) {
                nb = 0;
                idx = input.Length + 1;
            }
            if (nb > 0) {
                return new UoeExecutionOpenedgeException {
                    ErrorNumber = nb,
                    ErrorMessage = input.Substring(0, idx - 1)
                };
            }
            return null;
        }
    }
}