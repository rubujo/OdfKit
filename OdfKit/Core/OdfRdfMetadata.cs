using System;
using System.Collections.Generic;
using System.Linq;

namespace OdfKit.Core;

/// <summary>
/// 表示 ODF 封裝中 <c>META-INF/manifest.rdf</c> 的 RDF metadata 集合。
/// </summary>
public sealed class OdfRdfMetadata
{
    private readonly List<OdfRdfTriple> _triples = [];

    /// <summary>
    /// 取得目前記錄的 RDF triples。
    /// </summary>
    public IReadOnlyList<OdfRdfTriple> Triples => _triples;

    internal bool IsDirty { get; private set; }

    /// <summary>
    /// 新增一筆 RDF triple。
    /// </summary>
    /// <param name="subject">主詞 IRI。</param>
    /// <param name="predicate">述詞 IRI。</param>
    /// <param name="objectValue">受詞值。</param>
    /// <param name="isLiteral">受詞是否為 literal；若為 <see langword="false"/>，則視為資源 IRI。</param>
    /// <exception cref="ArgumentException">當任一必要值為空白時拋出。</exception>
    public void AddTriple(string subject, string predicate, string objectValue, bool isLiteral = true)
    {
        AddTriple(new OdfRdfTriple(subject, predicate, objectValue, isLiteral));
    }

    /// <summary>
    /// 新增一筆 RDF triple。
    /// </summary>
    /// <param name="triple">要新增的 RDF triple。</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="triple"/> 為 <see langword="null"/> 時拋出。</exception>
    public void AddTriple(OdfRdfTriple triple)
    {
        if (triple is null)
            throw new ArgumentNullException(nameof(triple));
        _triples.Add(triple);
        IsDirty = true;
    }

    /// <summary>
    /// 依主詞與述詞篩選 RDF triples。
    /// </summary>
    /// <param name="subject">主詞 IRI；為 <see langword="null"/> 時不篩選主詞。</param>
    /// <param name="predicate">述詞 IRI；為 <see langword="null"/> 時不篩選述詞。</param>
    /// <returns>符合條件的 triple 清單。</returns>
    public IReadOnlyList<OdfRdfTriple> FindTriples(string? subject = null, string? predicate = null)
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
    /// 嘗試取得指定主詞與述詞的 literal 受詞。
    /// </summary>
    /// <param name="subject">主詞 IRI。</param>
    /// <param name="predicate">述詞 IRI。</param>
    /// <param name="value">literal 受詞值。</param>
    /// <returns>若存在 literal triple 則為 <see langword="true"/>。</returns>
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
    /// 建立文件主詞與封裝組件之間的 <c>pkg:hasPart</c> 關聯。
    /// </summary>
    /// <param name="documentSubject">文件主詞 IRI（通常為空字串或 <c>./</c>）。</param>
    /// <param name="partPath">組件相對路徑。</param>
    public void LinkDocumentPart(string documentSubject, string partPath)
    {
        AddTriple(documentSubject, OdfPkgRdfPredicates.HasPart, partPath, isLiteral: false);
    }

    /// <summary>
    /// 設定封裝組件的 <c>pkg:mimeType</c> literal。
    /// </summary>
    /// <param name="partSubject">組件主詞 IRI。</param>
    /// <param name="mimeType">MIME 類型。</param>
    public void SetPartMimeType(string partSubject, string mimeType)
    {
        AddTriple(partSubject, OdfPkgRdfPredicates.MimeType, mimeType, isLiteral: true);
    }

    /// <summary>
    /// 移除符合主詞與述詞的 RDF triples。
    /// </summary>
    /// <param name="subject">主詞 IRI。</param>
    /// <param name="predicate">述詞 IRI；為 <see langword="null"/> 時移除該主詞的全部 triples。</param>
    /// <returns>移除的 triple 數量。</returns>
    public int RemoveTriples(string subject, string? predicate = null)
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
}

/// <summary>
/// 表示一筆 RDF triple。
/// </summary>
public sealed class OdfRdfTriple
{
    /// <summary>
    /// 以指定主詞、述詞與受詞建立新的 RDF triple。
    /// </summary>
    /// <param name="subject">主詞 IRI。</param>
    /// <param name="predicate">述詞 IRI。</param>
    /// <param name="objectValue">受詞值。</param>
    /// <param name="isLiteral">受詞是否為 literal；若為 <see langword="false"/>，則視為資源 IRI。</param>
    /// <exception cref="ArgumentException">當任一必要值為空白時拋出。</exception>
    public OdfRdfTriple(string subject, string predicate, string objectValue, bool isLiteral)
    {
        if (subject is null)
            throw new ArgumentNullException(nameof(subject));
        if (string.IsNullOrWhiteSpace(predicate))
            throw new ArgumentException("RDF 述詞不可為空白。", nameof(predicate));
        if (string.IsNullOrWhiteSpace(objectValue))
            throw new ArgumentException("RDF 受詞不可為空白。", nameof(objectValue));

        Subject = subject;
        Predicate = predicate;
        ObjectValue = objectValue;
        IsLiteral = isLiteral;
    }

    /// <summary>
    /// 取得主詞 IRI。
    /// </summary>
    public string Subject { get; }

    /// <summary>
    /// 取得述詞 IRI。
    /// </summary>
    public string Predicate { get; }

    /// <summary>
    /// 取得受詞值。
    /// </summary>
    public string ObjectValue { get; }

    /// <summary>
    /// 取得受詞是否為 literal。
    /// </summary>
    public bool IsLiteral { get; }
}
