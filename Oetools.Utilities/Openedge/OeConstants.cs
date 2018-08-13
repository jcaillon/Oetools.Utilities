#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (OeConstants.cs) is part of Oetools.Utilities.
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

namespace Oetools.Utilities.Openedge {
    public static class OeConstants {
        public const string ExtR = ".r";
        public const string ExtCompileErrorsLog = ".celog";
        public const string ExtDebugList = ".dbg";
        public const string ExtListing = ".lis";
        public const string ExtXref = ".xrf";
        public const string ExtXrefXml = ".xrf.xml";
        public const string ExtPreprocessed = ".preprocessed";
        public const string ExtCls = ".cls";
        public const string ExtFileIdLog = ".fileidlog";
        public const string ExtTableList = ".tablelist";
        
        public const string ExtProlibFile = ".pl";
        
        public const string ProwinWindowClass = "ProMainWin";
        
        public const string CompilableExtensionsPattern = "*.p,*.w,*.t,*.cls";
        
        public const string OeProjectExtension = ".oeproj";

        public const int MaximumCharacterLength = 31990;
        public const int MaximumPropathLength = 31990;
    }
}