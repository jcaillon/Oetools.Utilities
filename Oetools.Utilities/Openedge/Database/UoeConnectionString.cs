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

using System.Collections.Generic;
using System.Text;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Lib.ParameterStringParser;

namespace Oetools.Utilities.Openedge.Database {

    /// <summary>
    /// A connection string to connect a database.
    /// </summary>
    public class UoeConnectionString {

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
        /// Extra connection options, listed in 'Client database connection parameters':
        /// https://documentation.progress.com/output/ua/OpenEdge_latest/index.html#page/dpspr%2Fclient-database-connection-parameters.html%23.
        /// </summary>
        public StringBuilder ExtraOptions { get; } = new StringBuilder();

        protected UoeConnectionString() {}

        /// <summary>
        /// Make sure the connection string is valid.
        /// </summary>
        /// <exception cref="UoeConnectionStringParseException"></exception>
        private void Validate(string connectionString) {
            if (DatabaseLocation == null) {
                throw new UoeConnectionStringParseException($"Expecting to find at least one -db option in the connection string: {connectionString.PrettyQuote()}.");
            }
            if (SingleUser && (!string.IsNullOrEmpty(Service) || !string.IsNullOrEmpty(HostName))) {
                throw new UoeConnectionStringParseException($"The single user mode (-1) can't be used with -S or -H options in the connection string: {connectionString.PrettyQuote()}.");
            }
        }

        /// <summary>
        /// string representation of the connection string. To use in a CONNECT statement or as prowin parameters.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            var result = new StringBuilder();
            result.Append("-db ").Append(string.IsNullOrEmpty(Service) ? DatabaseLocation.FullPath.CliQuoter() : DatabaseLocation.PhysicalName);
            if (!string.IsNullOrEmpty(LogicalName)) {
                result.Append(" -ld ").Append(LogicalName);
            }
            if (!string.IsNullOrEmpty(Service)) {
                if (!string.IsNullOrEmpty(HostName)) {
                    result.Append(" -H ").Append(HostName);
                }
                result.Append(" -S ").Append(Service);
            }
            if (SingleUser) {
                result.Append(" -1").Append(Service);
            }
            if (ExtraOptions.Length > 0) {
                result.Append(' ').Append(ExtraOptions);
            }

            return result.TrimEnd().ToString();
        }

        /// <summary>
        /// Returns a connection string composed of several connection strings.
        /// </summary>
        /// <param name="connectionStrings"></param>
        /// <returns></returns>
        public static string GetConnectionString(IEnumerable<UoeConnectionString> connectionStrings) {
            var result = new StringBuilder();
            foreach (var connectionString in connectionStrings) {
                result.Append(connectionString).Append(' ');
            }
            return result.TrimEnd().ToString();
        }

        /// <summary>
        /// New connection string for single user mode.
        /// </summary>
        /// <param name="databaseLocation"></param>
        /// <param name="logicalName"></param>
        /// <returns></returns>
        public static UoeConnectionString NewSingleUserConnection(UoeDatabaseLocation databaseLocation, string logicalName = null) {
            return new UoeConnectionString {
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
        public static UoeConnectionString NewMultiUserConnection(UoeDatabaseLocation databaseLocation, string logicalName = null, string host = null, string service = null) {
            return new UoeConnectionString {
                DatabaseLocation = databaseLocation,
                LogicalName = logicalName,
                HostName = host,
                Service = service
            };
        }

        /// <summary>
        /// Returns one or more <see cref="UoeConnectionString"/> parsed from the given <paramref name="connectionString"/>.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        /// <exception cref="UoeConnectionStringParseException"></exception>
        public static IEnumerable<UoeConnectionString> GetConnectionStrings(string connectionString) {
            if (string.IsNullOrEmpty(connectionString))
                yield break;

            var tokenizer = new UoeParameterTokenizer(connectionString);

            bool lastTokenWasValue = false;
            var currentConnectionString = new UoeConnectionString();
            do {
                var token = tokenizer.PeekAtToken(0);
                if (token is ParameterStringTokenValue) {
                    if (lastTokenWasValue) {
                        throw new UoeConnectionStringParseException($"The value {token.Value.PrettyQuote()} does not seem to belong to any option in the connection string: {connectionString.PrettyQuote()}.");
                    }
                    lastTokenWasValue = true;
                    currentConnectionString.ExtraOptions.Append(token.Value).Append(' ');
                    continue;
                }
                lastTokenWasValue = false;
                if (token is ParameterStringTokenOption) {
                    switch (token.Value) {
                        case "-db":
                            if (currentConnectionString.DatabaseLocation != null) {
                                currentConnectionString.Validate(connectionString);
                                currentConnectionString.ExtraOptions.TrimEnd();
                                yield return currentConnectionString;
                                currentConnectionString = new UoeConnectionString();
                            }
                            var dbName = tokenizer.MoveAndPeekAtToken(2);
                            if (dbName is ParameterStringTokenValue) {
                                currentConnectionString.DatabaseLocation = new UoeDatabaseLocation(dbName.Value.StripQuotes());
                            } else {
                                throw new UoeConnectionStringParseException($"Expecting a database name or database path after the -db option in the connection string: {connectionString.PrettyQuote()}.");
                            }
                            break;
                        case "-ld":
                            var logicalName = tokenizer.MoveAndPeekAtToken(2);
                            if (logicalName is ParameterStringTokenValue) {
                                currentConnectionString.LogicalName = logicalName.Value.StripQuotes();
                            } else {
                                throw new UoeConnectionStringParseException($"Expecting a logical name after the -ld option in the connection string: {connectionString.PrettyQuote()}.");
                            }
                            break;
                        case "-1":
                            currentConnectionString.SingleUser = true;
                            break;
                        case "-S":
                            var serviceName = tokenizer.MoveAndPeekAtToken(2);
                            if (serviceName is ParameterStringTokenValue) {
                                currentConnectionString.Service = serviceName.Value.StripQuotes();
                            } else {
                                throw new UoeConnectionStringParseException($"Expecting a service name or port number after the -S option in the connection string: {connectionString.PrettyQuote()}.");
                            }
                            break;
                        case "-H":
                            var hostName = tokenizer.MoveAndPeekAtToken(2);
                            if (hostName is ParameterStringTokenValue) {
                                currentConnectionString.HostName = hostName.Value.StripQuotes();
                            } else {
                                throw new UoeConnectionStringParseException($"Expecting a host name after the -H option in the connection string: {connectionString.PrettyQuote()}.");
                            }
                            break;
                        default:
                            currentConnectionString.ExtraOptions.Append(token.Value).Append(' ');
                            break;
                    }
                }
            } while (tokenizer.MoveToNextToken());

            currentConnectionString.Validate(connectionString);
            currentConnectionString.ExtraOptions.TrimEnd();
            yield return currentConnectionString;
        }
    }
}
