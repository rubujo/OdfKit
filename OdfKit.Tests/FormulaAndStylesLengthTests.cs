using System;
using OdfKit.Core;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests
{
    public partial class FormulaAndStylesTest
    {
        #region OdfLength Tests

        /// <summary>
        /// 驗證 <see cref="OdfLength"/> 的工廠方法（FromPixels／FromPercentage／FromEm）能正確建立
        /// 對應單位的結構，且 <see cref="OdfLength.ConvertTo"/> 在絕對單位之間換算正確（以點為樞紐），
        /// 對相對單位（百分比／Em）與絕對單位互轉則擲出 <see cref="InvalidOperationException"/>。
        /// </summary>
        [Fact]
        public void TestOdfLengthFactoryMethodsAndConvertToHandlesAbsoluteAndRelativeUnits()
        {
            OdfLength pixels = OdfLength.FromPixels(96);
            Assert.Equal(OdfUnit.Pixels, pixels.Unit);
            Assert.Equal(96, pixels.Value);

            OdfLength percentage = OdfLength.FromPercentage(50);
            Assert.Equal(OdfUnit.Percentage, percentage.Unit);

            OdfLength em = OdfLength.FromEm(1.5);
            Assert.Equal(OdfUnit.Em, em.Unit);

            // 96 像素於標準 96 DPI 換算下應等於 1 英吋（72 點）。
            Assert.Equal(72.0, pixels.ConvertTo(OdfUnit.Points), precision: 6);
            Assert.Equal(1.0, pixels.ToInches(), precision: 6);

            OdfLength oneInch = OdfLength.FromInches(1.0);
            Assert.Equal(2.54, oneInch.ToCentimeters(), precision: 6);
            Assert.Equal(25.4, oneInch.ToMillimeters(), precision: 6);

            Assert.Throws<InvalidOperationException>(() => percentage.ConvertTo(OdfUnit.Centimeters));
            Assert.Throws<InvalidOperationException>(() => em.ConvertTo(OdfUnit.Points));
        }

        /// <summary>
        /// 驗證 <see cref="OdfLength.FallbackTo"/> 僅在單位為 <see cref="OdfUnit.Unspecified"/> 時
        /// 套用指定的預設單位，已有明確單位的長度則維持原樣不變。
        /// </summary>
        [Fact]
        public void TestOdfLengthFallbackToOnlyAppliesWhenUnspecified()
        {
            OdfLength unspecified = new(5, OdfUnit.Unspecified);
            OdfLength fallenBack = unspecified.FallbackTo(OdfUnit.Centimeters);
            Assert.Equal(OdfUnit.Centimeters, fallenBack.Unit);
            Assert.Equal(5, fallenBack.Value);

            OdfLength explicitCm = OdfLength.FromCentimeters(3);
            OdfLength unchanged = explicitCm.FallbackTo(OdfUnit.Millimeters);
            Assert.Equal(OdfUnit.Centimeters, unchanged.Unit);
            Assert.Equal(3, unchanged.Value);
        }

        /// <summary>
        /// 驗證 <see cref="OdfLength.GetHashCode"/> 與 <see cref="OdfLength.Equals(OdfLength)"/> 的雜湊碼契約：
        /// 不同絕對單位但數值相等的長度（例如 1 英吋與 2.54 公分）應視為相等且雜湊碼相同；
        /// 相對單位（百分比）則維持原單位雜湊，不與絕對單位混淆。
        /// </summary>
        [Fact]
        public void TestOdfLengthGetHashCodeIsConsistentWithCrossUnitEquality()
        {
            OdfLength oneInch = OdfLength.FromInches(1.0);
            OdfLength equivalentCm = OdfLength.FromCentimeters(2.54);

            Assert.True(oneInch.Equals(equivalentCm));
            Assert.Equal(oneInch.GetHashCode(), equivalentCm.GetHashCode());

            OdfLength fiftyPercent = OdfLength.FromPercentage(50);
            OdfLength otherFiftyPercent = OdfLength.FromPercentage(50);
            Assert.True(fiftyPercent.Equals(otherFiftyPercent));
            Assert.Equal(fiftyPercent.GetHashCode(), otherFiftyPercent.GetHashCode());
        }

        /// <summary>
        /// 驗證 <see cref="OdfBorder.GetHashCode"/> 與 <see cref="OdfBorder.Equals(OdfBorder)"/> 的雜湊碼契約：
        /// 樣式、寬度、色彩皆相等的框線應產生相同雜湊碼；任一欄位不同則雜湊碼應有極高機率不同。
        /// </summary>
        [Fact]
        public void TestOdfBorderGetHashCodeIsConsistentWithEquals()
        {
            OdfBorder solidBlack = OdfBorder.Parse("1pt solid #000000");
            OdfBorder anotherSolidBlack = OdfBorder.Parse("1pt solid #000000");
            Assert.True(solidBlack.Equals(anotherSolidBlack));
            Assert.Equal(solidBlack.GetHashCode(), anotherSolidBlack.GetHashCode());

            OdfBorder dashedRed = OdfBorder.Parse("2pt dashed #FF0000");
            Assert.False(solidBlack.Equals(dashedRed));
            Assert.NotEqual(solidBlack.GetHashCode(), dashedRed.GetHashCode());

            Assert.Equal(OdfBorder.None.GetHashCode(), OdfBorder.None.GetHashCode());
        }

        #endregion
    }
}
