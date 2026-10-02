using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Compliance;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 schema 內容比對使用的位元集合 <see cref="OdfPositionSet"/>：與 <see cref="HashSet{T}"/> 的行為一致。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Regression)]
public sealed class OdfPositionSetTests
{
    /// <summary>
    /// 驗證加入、重複加入、包含與數量。
    /// </summary>
    [Fact]
    public void AddReportsNewMembersAndCounts()
    {
        var set = new OdfPositionSet();
        Assert.Equal(0, set.Count);
        Assert.False(set.Contains(0));

        Assert.True(set.Add(5));
        Assert.False(set.Add(5));
        Assert.True(set.Add(0));
        Assert.True(set.Add(64));
        Assert.True(set.Add(63));

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(63));
        Assert.True(set.Contains(64));
        Assert.False(set.Contains(1));
        Assert.False(set.Contains(-1));
        Assert.Equal([0, 5, 63, 64], set.ToArray());
    }

    /// <summary>
    /// 驗證位置遠離零時記憶體只依跨度成長，且往較小位置擴張仍正確。
    /// </summary>
    [Fact]
    public void SetsHandleLargeAndDescendingPositions()
    {
        var set = new OdfPositionSet();
        foreach (int position in new[] { 100_000, 99_999, 70_000, 5, 1_000_000 })
        {
            Assert.True(set.Add(position));
        }

        Assert.Equal([5, 70_000, 99_999, 100_000, 1_000_000], set.ToArray());
        Assert.False(set.Contains(6));
        Assert.True(set.Contains(1_000_000));
    }

    /// <summary>
    /// 驗證合併不同跨度的集合，結果與以 <see cref="HashSet{T}"/> 計算的相同，來源集合不被修改。
    /// </summary>
    [Fact]
    public void UnionWithMatchesHashSetSemantics()
    {
        var random = new Random(20261001);
        for (int round = 0; round < 200; round++)
        {
            var expected = new HashSet<int>();
            var left = new OdfPositionSet();
            var right = new OdfPositionSet();
            int leftBase = random.Next(0, 500);
            int rightBase = random.Next(0, 500);
            for (int i = 0; i < random.Next(0, 40); i++)
            {
                int value = leftBase + random.Next(0, 300);
                expected.Add(value);
                left.Add(value);
            }

            var rightValues = new List<int>();
            for (int i = 0; i < random.Next(0, 40); i++)
            {
                int value = rightBase + random.Next(0, 300);
                expected.Add(value);
                right.Add(value);
                rightValues.Add(value);
            }

            left.UnionWith(right);

            Assert.Equal(expected.OrderBy(value => value).ToArray(), left.ToArray());
            Assert.Equal(expected.Count, left.Count);
            Assert.Equal(rightValues.Distinct().OrderBy(value => value).ToArray(), right.ToArray());
        }
    }

    /// <summary>
    /// 驗證與空集合、自身合併不改變內容。
    /// </summary>
    [Fact]
    public void UnionWithEmptyAndSelfIsNoOp()
    {
        var set = new OdfPositionSet { 3, 9 };
        set.UnionWith(new OdfPositionSet());
        set.UnionWith(set);
        Assert.Equal([3, 9], set.ToArray());

        var empty = new OdfPositionSet();
        empty.UnionWith(set);
        Assert.Equal([3, 9], empty.ToArray());
        Assert.Equal(2, empty.Count);
    }

    /// <summary>
    /// 驗證複製出的集合與原集合互不影響。
    /// </summary>
    [Fact]
    public void CloneIsIndependentOfTheOriginal()
    {
        var original = new OdfPositionSet { 3, 70, 200 };
        OdfPositionSet copy = original.Clone();
        copy.Add(5);
        original.Add(300);

        Assert.Equal([3, 5, 70, 200], copy.ToArray());
        Assert.Equal([3, 70, 200, 300], original.ToArray());
        Assert.Equal(4, copy.Count);
        Assert.Empty(new OdfPositionSet().Clone());
    }

    /// <summary>
    /// 驗證超集合判斷與差集：基準位置不同、跨字組與空集合的情形都與 <see cref="HashSet{T}"/> 一致。
    /// </summary>
    [Fact]
    public void SupersetAndDifferenceMatchHashSetSemantics()
    {
        int[][] samples =
        [
            [],
            [0],
            [5, 9],
            [5, 9, 64, 130],
            [64, 65],
            [130, 700],
            [1, 5, 9, 64, 130, 700],
        ];

        foreach (int[] left in samples)
        {
            foreach (int[] right in samples)
            {
                var leftSet = new OdfPositionSet();
                foreach (int value in left)
                {
                    leftSet.Add(value);
                }

                var rightSet = new OdfPositionSet();
                foreach (int value in right)
                {
                    rightSet.Add(value);
                }

                var leftReference = new HashSet<int>(left);
                Assert.Equal(leftReference.IsSupersetOf(right), leftSet.IsSupersetOf(rightSet));
                Assert.Equal(
                    left.Where(value => !right.Contains(value)).OrderBy(value => value).ToArray(),
                    leftSet.PositionsNotIn(rightSet).ToArray());
            }
        }
    }

    /// <summary>
    /// 驗證連續區間的精簡表示：逐一附加相鄰位置、與相鄰或重疊的區間合併都維持區間，
    /// 加入不相鄰的位置時轉成位元圖而內容不變。
    /// </summary>
    [Fact]
    public void ContiguousRunsStayCorrectWhenGrowingAndWhenConvertedToBitmap()
    {
        var set = new OdfPositionSet();
        for (int position = 100; position < 300; position++)
        {
            Assert.True(set.Add(position));
        }

        Assert.False(set.Add(150));
        Assert.True(set.Add(99));
        Assert.Equal(201, set.Count);
        Assert.Equal(Enumerable.Range(99, 201).ToArray(), set.ToArray());

        var adjacent = new OdfPositionSet();
        for (int position = 300; position < 320; position++)
        {
            adjacent.Add(position);
        }

        set.UnionWith(adjacent);
        Assert.Equal(Enumerable.Range(99, 221).ToArray(), set.ToArray());

        Assert.True(set.Add(1000));
        Assert.Equal(222, set.Count);
        Assert.True(set.Contains(1000));
        Assert.True(set.Contains(99));
        Assert.True(set.Contains(319));
        Assert.False(set.Contains(500));
        Assert.Equal(Enumerable.Range(99, 221).Append(1000).ToArray(), set.ToArray());
    }

    /// <summary>
    /// 以固定種子的隨機操作比對 <see cref="HashSet{T}"/>：位置集合混合連續區間與零散位置，
    /// 聯集、複製、超集合判斷、差集、包含與列舉在區間與位元圖兩種表示之間都必須一致。
    /// </summary>
    [Fact]
    public void RandomOperationsMatchHashSetAcrossRangeAndBitmapRepresentations()
    {
        var random = new Random(20260502);

        (OdfPositionSet Set, HashSet<int> Reference) Build()
        {
            var set = new OdfPositionSet();
            var reference = new HashSet<int>();
            int runs = random.Next(0, 4);
            for (int run = 0; run < runs; run++)
            {
                int start = random.Next(0, 400);
                int length = random.Next(1, 150);
                for (int position = start; position < start + length; position++)
                {
                    Assert.Equal(reference.Add(position), set.Add(position));
                }
            }

            int scattered = random.Next(0, 4);
            for (int index = 0; index < scattered; index++)
            {
                int position = random.Next(0, 700);
                Assert.Equal(reference.Add(position), set.Add(position));
            }

            return (set, reference);
        }

        for (int iteration = 0; iteration < 600; iteration++)
        {
            (OdfPositionSet left, HashSet<int> leftReference) = Build();
            (OdfPositionSet right, HashSet<int> rightReference) = Build();

            Assert.Equal(leftReference.Count, left.Count);
            Assert.Equal(leftReference.OrderBy(value => value).ToArray(), left.ToArray());
            Assert.Equal(leftReference.IsSupersetOf(rightReference), left.IsSupersetOf(right));
            Assert.Equal(
                leftReference.Where(value => !rightReference.Contains(value)).OrderBy(value => value).ToArray(),
                left.PositionsNotIn(right).ToArray());
            for (int probe = 0; probe < 12; probe++)
            {
                int position = random.Next(-2, 720);
                Assert.Equal(leftReference.Contains(position), left.Contains(position));
            }

            int[] snapshot = left.ToArray();
            OdfPositionSet copy = left.Clone();
            copy.UnionWith(right);
            leftReference.UnionWith(rightReference);
            Assert.Equal(leftReference.Count, copy.Count);
            Assert.Equal(leftReference.OrderBy(value => value).ToArray(), copy.ToArray());
            Assert.True(copy.IsSupersetOf(left));
            Assert.True(copy.IsSupersetOf(right));

            // 複製與聯集不得影響原集合。
            Assert.Equal(snapshot, left.ToArray());
            Assert.Equal(snapshot.Length, left.Count);
        }
    }

    /// <summary>
    /// 驗證負的位置會被拒絕。
    /// </summary>
    [Fact]
    public void NegativePositionsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OdfPositionSet().Add(-1));
    }
}
