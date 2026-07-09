using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Text;
using OdfKit.Core;
using OdfKit.Spreadsheet;

namespace OdfKit.DOM;

/// <summary>
/// Partial: lightweight cell view enumeration for TableTableElement.
/// Partial：TableTableElement 的輕量儲存格檢視列舉。
/// </summary>
public partial class TableTableElement
{
    /// <summary>
    /// Stack-only enumerable source returned by <see cref="EnumerateCellViews"/>.
    /// 由 <see cref="EnumerateCellViews"/> 傳回的 stack-only 列舉來源。
    /// </summary>
    public readonly ref struct OdfCellViewEnumerable
    {
        private readonly TableTableElement _table;

        internal OdfCellViewEnumerable(TableTableElement table)
        {
            _table = table;
        }

        /// <summary>
        /// Gets the cell-view enumerator.
        /// 取得儲存格檢視列舉器。
        /// </summary>
        /// <returns>The enumerator. / 儲存格檢視列舉器。</returns>
        public OdfCellViewEnumerator GetEnumerator()
            => new(_table);
    }

    /// <summary>
    /// Provides the OdfCellViewEnumerator API.
    /// 表示 <see cref="TableTableElement.EnumerateCellViews"/> 使用的 stack-only 儲存格檢視列舉器。
    /// </summary>
    public ref struct OdfCellViewEnumerator
    {
        private readonly TableTableElement _table;
        private readonly int _sparseMaxRow;
        private readonly int _sparseMaxColumn;
        private OdfCellViewEnumerationPhase _phase;
        private int _sparseRow;
        private int _sparseColumn;
        private OdfNode? _domRowNode;
        private OdfNode? _domCellNode;
        private int _domRowIndex;
        private int _domColumnIndex;
        private int _domRowRepeatIndex;
        private int _domRowRepeatCount;
        private int _domCellRepeatIndex;
        private int _domCellRepeatCount;

        internal OdfCellViewEnumerator(TableTableElement table)
        {
            _table = table;
            table.GetSparseCellBounds(out _sparseMaxRow, out _sparseMaxColumn);
            _phase = _sparseMaxRow >= 0 && _sparseMaxColumn >= 0
                ? OdfCellViewEnumerationPhase.Sparse
                : OdfCellViewEnumerationPhase.Dom;
            _sparseRow = 0;
            _sparseColumn = -1;
            _domRowNode = null;
            _domCellNode = null;
            _domRowIndex = -1;
            _domColumnIndex = -1;
            _domRowRepeatIndex = 0;
            _domRowRepeatCount = 0;
            _domCellRepeatIndex = 0;
            _domCellRepeatCount = 0;
            Current = default;
        }

        /// <summary>
        /// Gets the Current value.
        /// 取得目前的儲存格檢視。
        /// </summary>
        public OdfCellView Current { get; private set; }

        /// <summary>
        /// Performs move next.
        /// 移至下一個儲存格檢視。
        /// </summary>
        /// <returns>若成功移至下一筆資料則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
        public bool MoveNext()
        {
            if (_phase == OdfCellViewEnumerationPhase.Sparse && MoveNextSparse())
            {
                return true;
            }

            if (_phase == OdfCellViewEnumerationPhase.Sparse)
            {
                _phase = OdfCellViewEnumerationPhase.Dom;
            }

            if (_phase == OdfCellViewEnumerationPhase.Dom && MoveNextDom())
            {
                return true;
            }

            _phase = OdfCellViewEnumerationPhase.Done;
            return false;
        }

        private bool MoveNextSparse()
        {
            while (_sparseRow <= _sparseMaxRow)
            {
                _sparseColumn++;
                if (_sparseColumn > _sparseMaxColumn)
                {
                    _sparseColumn = -1;
                    _sparseRow++;
                    continue;
                }

                if (_table.TryGetSparseCellView(_sparseRow, _sparseColumn, out OdfCellView view))
                {
                    Current = view;
                    return true;
                }
            }

            return false;
        }

        private bool MoveNextDom()
        {
            while (true)
            {
                if (_domRowNode is null)
                {
                    if (!MoveToNextDomRow(_table.FirstChild))
                    {
                        return false;
                    }
                }

                if (_domCellNode is null)
                {
                    OdfNode currentRowNode = _domRowNode!;
                    _domCellNode = currentRowNode.FirstChild;
                    _domColumnIndex = -1;
                    _domCellRepeatIndex = 0;
                    _domCellRepeatCount = 0;
                }

                while (_domCellNode is not null)
                {
                    if (_domCellNode is TableTableCellElement cell)
                    {
                        if (_domCellRepeatCount == 0)
                        {
                            _domCellRepeatCount = GetPositiveRepeat(cell, "number-columns-repeated");
                            _domCellRepeatIndex = 0;
                        }

                        if (_domCellRepeatIndex < _domCellRepeatCount)
                        {
                            _domCellRepeatIndex++;
                            _domColumnIndex++;
                            Current = CreateDomCellView(_domRowIndex, _domColumnIndex, cell);
                            return true;
                        }
                    }

                    _domCellNode = _domCellNode.NextSibling;
                    _domCellRepeatIndex = 0;
                    _domCellRepeatCount = 0;
                }

                if (_domRowRepeatIndex + 1 < _domRowRepeatCount)
                {
                    OdfNode currentRowNode = _domRowNode!;
                    _domRowRepeatIndex++;
                    _domRowIndex++;
                    _domCellNode = currentRowNode.FirstChild;
                    _domColumnIndex = -1;
                    _domCellRepeatIndex = 0;
                    _domCellRepeatCount = 0;
                    continue;
                }

                OdfNode completedRowNode = _domRowNode!;
                if (!MoveToNextDomRow(completedRowNode.NextSibling))
                {
                    return false;
                }
            }
        }

        private bool MoveToNextDomRow(OdfNode? start)
        {
            for (OdfNode? node = start; node is not null; node = node.NextSibling)
            {
                if (node is not TableTableRowElement row)
                {
                    continue;
                }

                _domRowNode = row;
                _domRowRepeatIndex = 0;
                _domRowRepeatCount = GetPositiveRepeat(row, "number-rows-repeated");
                _domRowIndex++;
                _domCellNode = row.FirstChild;
                _domColumnIndex = -1;
                _domCellRepeatIndex = 0;
                _domCellRepeatCount = 0;
                return true;
            }

            _domRowNode = null;
            _domCellNode = null;
            return false;
        }
    }

    private enum OdfCellViewEnumerationPhase
    {
        Sparse,
        Dom,
        Done,
    }
}
