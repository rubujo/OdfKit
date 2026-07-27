using System;
using System.Collections.Generic;
using System.Linq;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// Provides the OdfRdfMetadata API.
/// 表示 ODF 封裝中 <c>META-INF/manifest.rdf</c> 的 RDF metadata 集合。
/// </summary>
public sealed class OdfRdfMetadata
{
    private readonly List<OdfRdfTriple> _triples = [];

    /// <summary>
    /// Provides the Triples member.
    /// 取得目前記錄的 RDF triples。
    /// </summary>
    public IReadOnlyList<OdfRdfTriple> Triples => _triples;

    internal bool IsDirty { get; private set; }
    /// <summary>
    /// Short overload of AddTriple that accepts subject, predicate, and objectValue; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 subject、predicate 與 objectValue；其餘可選參數使用預設值並轉呼叫最長 AddTriple 多載。
    /// </summary>
    public void AddTriple(string subject, string predicate, string objectValue) => AddTriple(subject, predicate, objectValue, true);


    /// <summary>
    /// Adds triple.
    /// 新增一筆 RDF triple。
    /// </summary>
    /// <param name="subject">主詞 IRI</param>
    /// <param name="predicate">述詞 IRI</param>
    /// <param name="objectValue">受詞值</param>
    /// <param name="isLiteral">受詞是否為 literal；若為 <see langword="false"/>，則視為資源 IRI</param>
    /// <exception cref="ArgumentException">當任一必要值為空白時拋出</exception>
    public void AddTriple(string subject, string predicate, string objectValue, bool isLiteral)
    {
        AddTriple(new OdfRdfTriple(subject, predicate, objectValue, isLiteral));
    }


    /// <summary>
    /// Adds triple.
    /// 新增一筆 RDF triple。
    /// </summary>
    /// <param name="triple">要新增的 RDF triple</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="triple"/> 為 <see langword="null"/> 時拋出</exception>
    public void AddTriple(OdfRdfTriple triple)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(triple, nameof(triple));
        _triples.Add(triple);
        IsDirty = true;
    }
    /// <summary>
    /// Short overload of GetTriples that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：GetTriples 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public IReadOnlyList<OdfRdfTriple> GetTriples() => GetTriples(null, null);

    /// <summary>
    /// Short overload of GetTriples that accepts subject; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 subject；其餘可選參數使用預設值並轉呼叫最長 GetTriples 多載。
    /// </summary>
    public IReadOnlyList<OdfRdfTriple> GetTriples(string? subject) => GetTriples(subject, null);


    /// <summary>
    /// Gets RDF triples filtered by subject and predicate.
    /// 依主詞與述詞篩選 RDF triples。
    /// </summary>
    /// <param name="subject">The subject IRI, or <see langword="null"/> to include all subjects. / 主詞 IRI；為 <see langword="null"/> 時不篩選主詞。</param>
    /// <param name="predicate">The predicate IRI, or <see langword="null"/> to include all predicates. / 述詞 IRI；為 <see langword="null"/> 時不篩選述詞。</param>
    /// <returns>The matching triples. / 符合條件的 triple 清單。</returns>
    public IReadOnlyList<OdfRdfTriple> GetTriples(string? subject, string? predicate)
    {
        IEnumerable<OdfRdfTriple> query = _triples;
        if (subject is not null)
        {
            query = query.Where(triple => triple.Subject == subject);
        }

        if (predicate is not null)
        {
            query = query.Where(triple => triple.Predicate == predicate);
        }

        return query.ToArray();
    }


    /// <summary>
    /// Tries to get literal.
    /// 嘗試取得指定主詞與述詞的 literal 受詞。
    /// </summary>
    /// <param name="subject">主詞 IRI</param>
    /// <param name="predicate">述詞 IRI</param>
    /// <param name="value">literal 受詞值</param>
    /// <returns>若存在 literal triple 則為 <see langword="true"/></returns>
    public bool TryGetLiteral(string subject, string predicate, out string value)
    {
        OdfRdfTriple? triple = _triples.FirstOrDefault(candidate =>
            candidate.Subject == subject &&
            candidate.Predicate == predicate &&
            candidate.IsLiteral);
        if (triple is null)
        {
            value = string.Empty;
            return false;
        }

        value = triple.ObjectValue;
        return true;
    }

    /// <summary>
    /// Performs link document part.
    /// 建立文件主詞與封裝組件之間的 <c>pkg:hasPart</c> 關聯。
    /// </summary>
    /// <param name="documentSubject">文件主詞 IRI（通常為空字串或 <c>./</c>）</param>
    /// <param name="partPath">組件相對路徑</param>
    public void LinkDocumentPart(string documentSubject, string partPath)
    {
        AddTriple(documentSubject, OdfPkgRdfPredicates.HasPart, partPath, isLiteral: false);
    }

    /// <summary>
    /// Sets part mime type.
    /// 設定封裝組件的 <c>pkg:mimeType</c> literal。
    /// </summary>
    /// <param name="partSubject">組件主詞 IRI</param>
    /// <param name="mimeType">MIME 類型</param>
    public void SetPartMimeType(string partSubject, string mimeType)
    {
        AddTriple(partSubject, OdfPkgRdfPredicates.MimeType, mimeType, isLiteral: true);
    }
    /// <summary>
    /// Short overload of SyncWithPackageEntries that accepts entryPaths; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 entryPaths；其餘可選參數使用預設值並轉呼叫最長 SyncWithPackageEntries 多載。
    /// </summary>
    public int SyncWithPackageEntries(IEnumerable<string> entryPaths, IReadOnlyDictionary<string, string> mediaTypes) => SyncWithPackageEntries(entryPaths, mediaTypes, null);


    /// <summary>
    /// Performs sync with package entries.
    /// 依目前封裝項目同步 <c>pkg:hasPart</c> 與 <c>pkg:mimeType</c> 三元組。
    /// </summary>
    /// <param name="entryPaths">封裝項目路徑集合</param>
    /// <param name="mediaTypes">專案路徑對應的 MIME 類型對照表</param>
    /// <param name="documentSubject">文件主詞 IRI；為 <see langword="null"/> 時沿用既有 <c>pkg:hasPart</c> 主詞或空字串</param>
    /// <returns>新增或更新的 triple 數量</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="entryPaths"/> 或 <paramref name="mediaTypes"/> 為 <see langword="null"/> 時拋出</exception>
    public int SyncWithPackageEntries(IEnumerable<string> entryPaths, IReadOnlyDictionary<string, string> mediaTypes, string? documentSubject)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(entryPaths, nameof(entryPaths));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(mediaTypes, nameof(mediaTypes));

        string docSubject = ResolveDocumentSubject(documentSubject);
        HashSet<string> desiredParts = [];
        foreach (string path in entryPaths)
        {
            if (!ShouldSyncEntry(path))
                continue;

            desiredParts.Add(NormalizePartPath(path));
        }

        int changed = 0;
        changed += RemoveStaleHasParts(docSubject, desiredParts);
        changed += RemoveStaleMimeTypes(desiredParts);

        HashSet<string> linkedParts = new(GetLinkedPartPaths(docSubject), StringComparer.Ordinal);
        foreach (string partPath in desiredParts)
        {
            if (!linkedParts.Contains(partPath))
            {
                LinkDocumentPart(docSubject, partPath);
                linkedParts.Add(partPath);
                changed++;
            }

            if (!mediaTypes.TryGetValue(partPath, out string? mediaType) || string.IsNullOrEmpty(mediaType))
                continue;

            string partSubject = FindPartSubject(partPath) ?? partPath;
            if (TryGetLiteral(partSubject, OdfPkgRdfPredicates.MimeType, out string currentMimeType) &&
                currentMimeType == mediaType)
                continue;

            changed += RemoveTriples(partSubject, OdfPkgRdfPredicates.MimeType);
            SetPartMimeType(partSubject, mediaType);
            changed++;
        }

        return changed;
    }


    /// <summary>
    /// Gets linked part paths.
    /// 取得指定文件主詞已連結的封裝組件路徑。
    /// </summary>
    /// <param name="documentSubject">文件主詞 IRI</param>
    /// <returns>已正規化之組件路徑集合</returns>
    public IReadOnlyList<string> GetLinkedPartPaths(string documentSubject)
    {
        return GetTriples(documentSubject, OdfPkgRdfPredicates.HasPart)
            .Where(triple => !triple.IsLiteral)
            .Select(triple => NormalizePartPath(triple.ObjectValue))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
    /// <summary>
    /// Short overload of RemoveTriples that accepts subject; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 subject；其餘可選參數使用預設值並轉呼叫最長 RemoveTriples 多載。
    /// </summary>
    public int RemoveTriples(string subject) => RemoveTriples(subject, null);


    /// <summary>
    /// Removes triples.
    /// 移除符合主詞與述詞的 RDF triples。
    /// </summary>
    /// <param name="subject">主詞 IRI</param>
    /// <param name="predicate">述詞 IRI；為 <see langword="null"/> 時移除該主詞的全部 triples</param>
    /// <returns>移除的 triple 數量</returns>
    public int RemoveTriples(string subject, string? predicate)
    {
        int removed = _triples.RemoveAll(triple =>
            triple.Subject == subject &&
            (predicate is null || triple.Predicate == predicate));
        if (removed > 0)
        {
            IsDirty = true;
        }

        return removed;
    }


    /// <summary>
    /// Performs the Clear operation.
    /// 清除全部 RDF triples。
    /// </summary>
    public void Clear()
    {
        if (_triples.Count == 0)
        {
            return;
        }

        _triples.Clear();
        IsDirty = true;
    }

    internal void AddLoadedTriple(OdfRdfTriple triple)
    {
        _triples.Add(triple);
    }

    internal void AcceptChanges()
    {
        IsDirty = false;
    }

    private static bool ShouldSyncEntry(string path) =>
        !string.IsNullOrEmpty(path) &&
        !string.Equals(path, "mimetype", StringComparison.Ordinal) &&
        !string.Equals(path, "META-INF/manifest.xml", StringComparison.Ordinal) &&
        !string.Equals(path, "META-INF/manifest.rdf", StringComparison.Ordinal) &&
        !global::OdfKit.Internal.OdfStringHelper.EndsWith(path, '/');

    private static string NormalizePartPath(string path)
    {
        if (path.StartsWith("./", StringComparison.Ordinal))
            return path.Substring(2);
        return path;
    }

    private string ResolveDocumentSubject(string? documentSubject)
    {
        if (documentSubject is not null)
            return documentSubject;

        return _triples
            .Where(triple => triple.Predicate == OdfPkgRdfPredicates.HasPart)
            .GroupBy(triple => triple.Subject)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault() ?? string.Empty;
    }

    private int RemoveStaleHasParts(string documentSubject, HashSet<string> desiredParts)
    {
        List<OdfRdfTriple> staleTriples = _triples
            .Where(triple =>
                triple.Subject == documentSubject &&
                triple.Predicate == OdfPkgRdfPredicates.HasPart &&
                !triple.IsLiteral &&
                !desiredParts.Contains(NormalizePartPath(triple.ObjectValue)))
            .ToList();

        foreach (OdfRdfTriple triple in staleTriples)
            _triples.Remove(triple);

        if (staleTriples.Count > 0)
            IsDirty = true;

        return staleTriples.Count;
    }

    private int RemoveStaleMimeTypes(HashSet<string> desiredParts)
    {
        List<OdfRdfTriple> staleTriples = _triples
            .Where(triple =>
                triple.Predicate == OdfPkgRdfPredicates.MimeType &&
                triple.IsLiteral &&
                !desiredParts.Contains(NormalizePartPath(triple.Subject)))
            .ToList();

        foreach (OdfRdfTriple triple in staleTriples)
            _triples.Remove(triple);

        if (staleTriples.Count > 0)
            IsDirty = true;

        return staleTriples.Count;
    }

    private string? FindPartSubject(string normalizedPartPath)
    {
        OdfRdfTriple? triple = _triples.FirstOrDefault(candidate =>
            candidate.Predicate == OdfPkgRdfPredicates.MimeType &&
            candidate.IsLiteral &&
            NormalizePartPath(candidate.Subject) == normalizedPartPath);
        return triple?.Subject;
    }
}

/// <summary>
/// Provides the OdfRdfTriple API.
/// 表示一筆 RDF triple。
/// </summary>
public sealed class OdfRdfTriple
{
    /// <summary>
    /// Performs odf rdf triple.
    /// 以指定主詞、述詞與受詞建立新的 RDF triple。
    /// </summary>
    /// <param name="subject">主詞 IRI</param>
    /// <param name="predicate">述詞 IRI</param>
    /// <param name="objectValue">受詞值</param>
    /// <param name="isLiteral">受詞是否為 literal；若為 <see langword="false"/>，則視為資源 IRI</param>
    /// <exception cref="ArgumentException">當任一必要值為空白時拋出</exception>
    public OdfRdfTriple(string subject, string predicate, string objectValue, bool isLiteral)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(subject, nameof(subject));
        if (string.IsNullOrWhiteSpace(predicate))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfRdfMetadata_RdfCannotBeEmpty"), nameof(predicate));
        if (string.IsNullOrWhiteSpace(objectValue))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfRdfMetadata_RdfCannotBeEmpty_2"), nameof(objectValue));

        Subject = subject;
        Predicate = predicate;
        ObjectValue = objectValue;
        IsLiteral = isLiteral;
    }

    /// <summary>
    /// Gets the Subject value.
    /// 取得主詞 IRI。
    /// </summary>
    public string Subject { get; }

    /// <summary>
    /// Gets the Predicate value.
    /// 取得述詞 IRI。
    /// </summary>
    public string Predicate { get; }

    /// <summary>
    /// Gets the ObjectValue value.
    /// 取得受詞值。
    /// </summary>
    public string ObjectValue { get; }

    /// <summary>
    /// Gets a value indicating the IsLiteral state.
    /// 取得受詞是否為 literal。
    /// </summary>
    public bool IsLiteral { get; }
}
