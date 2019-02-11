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
using System.Diagnostics;
using System.Linq;

namespace Oetools.Utilities.Openedge.Database {

    /// <summary>
    /// Allows to start a database, do something and shut it down during disposal.
    /// </summary>
    public class UoeDatabaseStarted : IDisposable {

        /// <summary>
        /// Are we allowed to use a process kill instead of a classic proshut?
        /// </summary>
        public bool AllowsDatabaseShutdownWithKill { get; set; } = true;

        private UoeDatabaseOperator _operator;

        private UoeDatabaseLocation _targetDb;

        private UoeDatabaseConnection _connection;

        private int _processId;

        /// <summary>
        /// Starts the given database.
        /// </summary>
        /// <param name="operator"></param>
        /// <param name="targetDb"></param>
        /// <param name="sharedMemoryMode"></param>
        /// <param name="nbUsers"></param>
        /// <param name="options"></param>
        public UoeDatabaseStarted(UoeDatabaseOperator @operator, UoeDatabaseLocation targetDb, bool sharedMemoryMode = false, int? nbUsers = null, string options = null) {
            _operator = @operator;
            _targetDb = targetDb;

            _connection = UoeDatabaseConnection.NewMultiUserConnection(targetDb, null, sharedMemoryMode ? null : "localhost", sharedMemoryMode ? null : UoeDatabaseOperator.GetNextAvailablePort().ToString());

            var startTime = DateTime.Now;
            _operator.Start(_targetDb, _connection.HostName, _connection.Service, nbUsers, options);

            var newProcess = Process.GetProcesses()
                .Where(p => {
                    try {
                        return p.ProcessName.Contains("_mprosrv") && p.StartTime.CompareTo(startTime) > 0;
                    } catch (Exception) {
                        return false;
                    }
                })
                .OrderBy(p => p.StartTime)
                .FirstOrDefault();
            if (newProcess != null) {
                _processId = newProcess.Id;
            }
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
            if (AllowsDatabaseShutdownWithKill && _processId > 0 && _operator.KillBrokerServer(_processId, _targetDb)) {
                return;
            }
            _operator.Shutdown(_targetDb);
        }
    }
}
