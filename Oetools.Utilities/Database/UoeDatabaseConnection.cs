#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeConnectionString.cs) is part of Oetools.Utilities.
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
using DotUtilities;
using DotUtilities.Extensions;
using DotUtilities.ParameterString;
using DotUtilities.Process;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Openedge.Database {

    /// <summary>
    /// A connection string to connect a database.
    /// </summary>
    public class UoeDatabaseConnection : IProcessArgs {

        /// <summary>
        /// The database (-db name).
        /// </summary>
        public UoeDatabaseLocation DatabaseLocation { get; private set; }

        /// <summary>
        /// Logical name used in the connection (-ld name).
        /// </summary>
        public string LogicalName { get; private set; }

        /// <summary>
        /// Single user mode (-1).
        /// </summary>
        public bool SingleUser { get; private set; }

        /// <summary>
        /// Service name (-S name/port).
        /// </summary>
        public string Service { get; private set; }

        /// <summary>
        /// Hostname (-H name).
        /// </summary>
        public string HostName { get; private set; }
        /// <summary>
        /// Max connection try (-ct number).
        /// </summary>
        public int? MaxConnectionTry { get; set; }

        /// <summary>
        /// Userid (-U name).
        /// </summary>
        public string UserId { get; private set; }

        /// <summary>
        /// Password (-P name).
        /// </summary>
        public string Password { get; private set; }


        /// <summary>
        /// Extra connection options that were not parsed by this class, listed in 'Client database connection parameters' from the oe docs.
        /// </summary>
        /// <remarks>
        /// https://documentation.progress.com/output/ua/OpenEdge_latest/index.html#page/dpspr%2Fclient-database-connection-parameters.html%23.
        /// </remarks>
        public UoeProcessArgs ExtraOptions { get; } = new UoeProcessArgs();

        protected UoeDatabaseConnection() {}

        /// <summary>
        /// Make sure the connection string is valid.
        /// </summary>
        /// <exception cref="UoeDatabaseConnectionParseException"></exception>
        private void Validate(string connectionString) {
            if (DatabaseLocation == null) {
                throw new UoeDatabaseConnectionParseException($"Expecting to find at least one -db option in the connection string: {connectionString.PrettyQuote()}.");
            }
            if (SingleUser && (!string.IsNullOrEmpty(Service) || !string.IsNullOrEmpty(HostName))) {
                throw new UoeDatabaseConnectionParseException($"The single user mode (-1) can't be used with -S or -H options in the connection string: {connectionString.PrettyQuote()}.");
            }
        }

        /// <summary>
        /// string representation of the connection string. To use in a CONNECT statement.
        /// </summary>
        /// <returns></returns>
        public string ToJdbcConnectionArgument(bool includeUserId) {
            var login = includeUserId ? $"{(!string.IsNullOrEmpty(UserId) ? $",{UserId}": "")}{(!string.IsNullOrEmpty(Password) ? $",{Password}": "")}" : null;
            return $"progress:T:{HostName ?? "localhost"}:{Service}:{DatabaseLocation?.PhysicalName}{login}";
        }

        /// <summary>
        /// String representation of the connection string. To use in a CONNECT statement.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => ToArgs().ToQuotedArgs();

        /// <summary>
        /// Get the process arguments for the database connection
        /// </summary>
        /// <returns></returns>
        public ProcessArgs ToArgs() {
            var args = new UoeProcessArgs();
            args.Append("-db", string.IsNullOrEmpty(Service) ? DatabaseLocation.FullPath : DatabaseLocation.PhysicalName);
            if (!string.IsNullOrEmpty(LogicalName)) {
                args.Append("-ld", LogicalName);
            }
            if (!string.IsNullOrEmpty(Service)) {
                if (!string.IsNullOrEmpty(HostName)) {
                    args.Append("-H", HostName);
                }
                args.Append("-S", Service);
            }
            if (SingleUser) {
                args.Append("-1");
            }
            if (!string.IsNullOrEmpty(UserId)) {
                args.Append("-U", UserId);
                if (!string.IsNullOrEmpty(Password)) {
                    args.Append("-P", Password);
                }
            }
            if (MaxConnectionTry.HasValue) {
                args.Append("-ct", MaxConnectionTry.Value.ToString());
            }
            args.Append(ExtraOptions);
            return args;
        }

        /// <summary>
        /// New connection string for single user mode.
        /// </summary>
        /// <param name="databaseLocation"></param>
        /// <param name="logicalName"></param>
        /// <returns></returns>
        public static UoeDatabaseConnection NewSingleUserConnection(UoeDatabaseLocation databaseLocation, string logicalName = null) {
            return new UoeDatabaseConnection {
                DatabaseLocation = databaseLocation,
                LogicalName = logicalName,
                SingleUser = true
            };
        }

        /// <summary>
        /// New connection string for multi user mode.
        /// </summary>
        /// <param name="databaseLocation"></param>
        /// <param name="logicalName"></param>
        /// <param name="host"></param>
        /// <param name="service"></param>
        /// <returns></returns>
        public static UoeDatabaseConnection NewMultiUserConnection(UoeDatabaseLocation databaseLocation, string logicalName = null, string host = null, string service = null) {
            return new UoeDatabaseConnection {
                DatabaseLocation = databaseLocation,
                LogicalName = logicalName,
                HostName = host,
                Service = service
            };
        }

        /// <summary>
        /// Returns one or more <see cref="UoeDatabaseConnection"/> parsed from the given <paramref name="connectionString"/>.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseConnectionParseException"></exception>
        public static IEnumerable<UoeDatabaseConnection> GetConnectionStrings(string connectionString) {
            if (string.IsNullOrEmpty(connectionString))
                yield break;

            var tokenizer = ParameterStringTokenizer.New(connectionString);

            bool lastTokenWasValue = false;
            var currentConnectionString = new UoeDatabaseConnection();
            while (tokenizer.MoveToNextToken()) {
                var token = tokenizer.PeekAtToken(0);
                if (token is ParameterStringTokenValue) {
                    if (lastTokenWasValue) {
                        throw new UoeDatabaseConnectionParseException($"The value {token.Value.PrettyQuote()} does not seem to belong to any option in the connection string: {connectionString.PrettyQuote()}.");
                    }
                    lastTokenWasValue = true;
                    currentConnectionString.ExtraOptions.Append(token.Value);
                    continue;
                }
                lastTokenWasValue = false;
                if (token is ParameterStringTokenOption) {
                    switch (token.Value) {
                        case "-db":
                            if (currentConnectionString.DatabaseLocation != null) {
                                currentConnectionString.Validate(connectionString);
                                yield return currentConnectionString;
                                currentConnectionString = new UoeDatabaseConnection();
                            }
                            var dbName = tokenizer.MoveAndPeekAtToken(2);
                            if (dbName is ParameterStringTokenValue) {
                                currentConnectionString.DatabaseLocation = new UoeDatabaseLocation(dbName.Value);
                            } else {
                                throw new UoeDatabaseConnectionParseException($"Expecting a database name or database path after the -db option in the connection string: {connectionString.PrettyQuote()}.");
                            }
                            break;
                        case "-ld":
                            var logicalName = tokenizer.MoveAndPeekAtToken(2);
                            if (logicalName is ParameterStringTokenValue) {
                                currentConnectionString.LogicalName = logicalName.Value;
                            } else {
                                throw new UoeDatabaseConnectionParseException($"Expecting a logical name after the -ld option in the connection string: {connectionString.PrettyQuote()}.");
                            }
                            break;
                        case "-1":
                            currentConnectionString.SingleUser = true;
                            break;
                        case "-S":
                            var serviceName = tokenizer.MoveAndPeekAtToken(2);
                            if (serviceName is ParameterStringTokenValue) {
                                currentConnectionString.Service = serviceName.Value;
                            } else {
                                throw new UoeDatabaseConnectionParseException($"Expecting a service name or port number after the -S option in the connection string: {connectionString.PrettyQuote()}.");
                            }
                            break;
                        case "-H":
                            var hostName = tokenizer.MoveAndPeekAtToken(2);
                            if (hostName is ParameterStringTokenValue) {
                                currentConnectionString.HostName = hostName.Value;
                            } else {
                                throw new UoeDatabaseConnectionParseException($"Expecting a host name after the -H option in the connection string: {connectionString.PrettyQuote()}.");
                            }
                            break;
                        case "-U":
                            var userId = tokenizer.MoveAndPeekAtToken(2);
                            if (userId is ParameterStringTokenValue) {
                                currentConnectionString.UserId = userId.Value;
                            } else {
                                throw new UoeDatabaseConnectionParseException($"Expecting a userid after the -U option in the connection string: {connectionString.PrettyQuote()}.");
                            }
                            break;
                        case "-P":
                            var password = tokenizer.MoveAndPeekAtToken(2);
                            if (password is ParameterStringTokenValue) {
                                currentConnectionString.Password = password.Value;
                            } else {
                                throw new UoeDatabaseConnectionParseException($"Expecting a password after the -P option in the connection string: {connectionString.PrettyQuote()}.");
                            }
                            break;
                        case "-ct":
                            var maxTry = tokenizer.MoveAndPeekAtToken(2);
                            if (maxTry is ParameterStringTokenValue) {
                                try {
                                    currentConnectionString.MaxConnectionTry = int.Parse(maxTry.Value);
                                    break;
                                } catch (FormatException) {
                                    // throw later
                                }
                            }
                            throw new UoeDatabaseConnectionParseException($"Expecting a number value after the -ct option in the connection string: {connectionString.PrettyQuote()}.");
                        default:
                            currentConnectionString.ExtraOptions.Append(token.Value);
                            break;
                    }
                }
            }

            currentConnectionString.Validate(connectionString);
            yield return currentConnectionString;
        }
    }
}
