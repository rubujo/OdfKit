using System.Collections.Generic;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    /// <summary>
    /// 供封裝專案讀寫引擎使用的內部協作存取器。
    /// </summary>
    internal OdfPackageEntryCollaborators EntryCollaborators => new(this);

    /// <summary>
    /// 封裝專案讀寫管線的內部協作存取器。
    /// </summary>
    internal readonly struct OdfPackageEntryCollaborators
    {
        private readonly OdfPackage _package;

        internal OdfPackageEntryCollaborators(OdfPackage package) => _package = package;

        internal IDictionary<string, OdfPackageEntry> Entries
        {
            get
            {
                var pkg = _package;
                return pkg._inTransaction
                    ? new UndoableDictionary<string, OdfPackageEntry>(pkg, pkg._entries,
                        (key, old, existed) =>
                        {
                            if (existed && old != null)
                            {
                                // 在將舊 entry 放進撤銷日誌前，先將其二進位資料載入記憶體中，以防 MMF 釋放後失效
                                old.EnsureBytesLoaded();
                            }
                            pkg._undoLog.Add(new UndoSetEntry(key, old, existed));
                        },
                        (key, old) =>
                        {
                            if (old != null)
                            {
                                // 在將舊 entry 放進撤銷日誌前，先將其二進位資料載入記憶體中，以防 MMF 釋放後失效
                                old.EnsureBytesLoaded();
                            }
                            pkg._undoLog.Add(new UndoRemoveEntry(key, old!));
                        })
                    : pkg._entries;
            }
        }

        internal IDictionary<string, string> Manifest
        {
            get
            {
                var pkg = _package;
                return pkg._inTransaction
                    ? new UndoableDictionary<string, string>(pkg, pkg._manifest, (key, old, existed) => pkg._undoLog.Add(new UndoSetManifest(key, old, existed)), (key, old) => pkg._undoLog.Add(new UndoRemoveManifest(key, old)))
                    : pkg._manifest;
            }
        }

        internal IList<string> EntryOrder
        {
            get
            {
                var pkg = _package;
                return pkg._inTransaction
                    ? new UndoableList<string>(pkg, pkg._entryOrder, (oldList) => pkg._undoLog.Add(new UndoSetEntryOrder(oldList)))
                    : pkg._entryOrder;
            }
        }

        internal void SetMimeTypeValue(string mimetype) => _package._mimetype = mimetype;

        internal void RemoveOutdatedSignatures() => _package.RemoveOutdatedSignatures();
    }
}
