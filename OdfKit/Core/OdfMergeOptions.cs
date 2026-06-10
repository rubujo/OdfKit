using System;

namespace OdfKit.Core
{
    /// <summary>
    /// Specifies how style name conflicts are resolved when appending or merging documents.
    /// </summary>
    public enum ConflictResolution
    {
        /// <summary>
        /// Conflicting styles from the source document are renamed (e.g. MyStyle_s1) 
        /// and copied to keep the source document's formatting isolated.
        /// </summary>
        KeepSourceFormatting,

        /// <summary>
        /// Conflicting styles are discarded. Cloned source nodes will reference 
        /// the destination's style directly, aligning with the destination's theme.
        /// </summary>
        UseDestinationStyles
    }

    /// <summary>
    /// Configuration options for merging or appending documents.
    /// </summary>
    public class OdfMergeOptions
    {
        /// <summary>
        /// Gets or sets the style conflict resolution strategy.
        /// </summary>
        public ConflictResolution StyleConflictResolution { get; set; } = ConflictResolution.KeepSourceFormatting;

        /// <summary>
        /// Gets or sets a value indicating whether referenced media/images should be copied and migrated.
        /// </summary>
        public bool CopyMedia { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether custom styles should be imported.
        /// </summary>
        public bool ImportStyles { get; set; } = true;

        /// <summary>
        /// Gets the default merge options configuration.
        /// </summary>
        public static OdfMergeOptions Default => new OdfMergeOptions();
    }
}
