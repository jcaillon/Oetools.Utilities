namespace Oetools.Utilities.Archive {
    public interface IFileToDeployInPackage {
        
        /// <summary>
        ///     Need to deploy this file FROM this path
        /// </summary>
        string From { get; set; }

        /// <summary>
        ///     Path to the pack in which we need to include this file
        /// </summary>
        string PackPath { get; set; }

        /// <summary>
        ///     The relative path of the file within the pack
        /// </summary>
        string RelativePathInPack { get; set; }
    }
}