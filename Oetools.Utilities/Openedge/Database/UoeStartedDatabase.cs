#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeDatabaseStarted.cs) is part of Oetools.Utilities.
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
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Openedge.Database {

    /// <summary>
    /// Allows to start a database, do something and shut it down during disposal.
    /// </summary>
    public class UoeStartedDatabase : IDisposable {

        /// <summary>
        /// Are we allowed to use a process kill instead of a classic proshut?
        /// </summary>
        public bool AllowsDatabaseShutdownWithKill { get; set; } = true;

        private UoeDatabaseAdministrator _operator;

        private UoeDatabaseLocation _targetDb;

        private UoeDatabaseConnection _connection;

        private List<int> _processIds;

        /// <summary>
        /// Starts the given database.
        /// </summary>
        /// <param name="operator"></param>
        /// <param name="targetDb"></param>
        /// <param name="nbUsers"></param>
        /// <param name="sharedMemoryMode"></param>
        /// <param name="options"></param>
        public UoeStartedDatabase(UoeDatabaseAdministrator @operator, UoeDatabaseLocation targetDb, int nbUsers, bool sharedMemoryMode = false, UoeProcessArgs options = null) {
            _operator = @operator;
            _targetDb = targetDb;
            _connection = _operator.Start(_targetDb, nbUsers, out _processIds, sharedMemoryMode, options);
        }

        /// <summary>
        /// Get the connection for the started database.
        /// </summary>
        /// <returns></returns>
        public UoeDatabaseConnection GetDatabaseConnection() => _connection;

        /// <summary>
        /// Called on instance disposal.
        /// </summary>
        public void Dispose() {
            if (AllowsDatabaseShutdownWithKill && _processIds != null && _processIds.Count > 0) {
                bool allKilled = true;
                foreach (var processId in _processIds) {
                    allKilled = allKilled && _operator.KillBrokerServer(processId, _targetDb);
                }
                if (allKilled) {
                    return;
                }
            }
            _operator.Shutdown(_targetDb);
            _operator = null;
        }
    }
}
