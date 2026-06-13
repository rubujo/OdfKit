using System;
using System.Collections.Generic;

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
        if (triple is null) throw new ArgumentNullException(nameof(triple));
        _triples.Add(triple);
        IsDirty = true;
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
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("RDF 主詞不可為空白。", nameof(subject));
        if (string.IsNullOrWhiteSpace(predicate)) throw new ArgumentException("RDF 述詞不可為空白。", nameof(predicate));
        if (string.IsNullOrWhiteSpace(objectValue)) throw new ArgumentException("RDF 受詞不可為空白。", nameof(objectValue));

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
