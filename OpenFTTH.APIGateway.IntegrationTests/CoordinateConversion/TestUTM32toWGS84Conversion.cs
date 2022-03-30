using FluentAssertions;
using OpenFTTH.APIGateway.Util;
using System;
using Xunit;

namespace OpenFTTH.APIGateway.IntegrationTests.CoordinateConversion
{
    public class TestUTM32toWGS84Conversion
    {
        [Fact]
        public void TestWgs84ConversionAlgorithm()
        {
            var result = UTM32WGS84Converter.ConvertFromUTM32NToWGS84(552000, 6190000);

            Math.Round(result[0], 2).Should().Be(9.83);
            Math.Round(result[1], 2).Should().Be(55.85);
        }


        [Fact]
        public void TestLineStringConverter()
        {
            string[] lines = new string[] { "[[552000,6190000], [552001,6190001]]", "[[552000,6190000], [552001,6190001]]" };

            var result = UTM32WGS84Converter.ConvertGeoJsonLineStringsToWgs84(lines);

            result.Length.Should().Be(2);
            result[0].Should().Contain("9.83");
            result[0].Should().NotContain("{");
        }
    }
}
