using System.Numerics;
using System.Text.Json;
using QueueLab.Core.Common;
using Xunit;

namespace QueueLab.Tests;

/// <summary>
/// Checks the Python-compatibility helpers against values recorded from CPython / NumPy
/// (Golden/helper_golden.json). These are what make seeded results identical to the original backend.
/// </summary>
public class HelperGoldenTests
{
    private static readonly JsonElement Golden = JsonDocument
        .Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden", "helper_golden.json")))
        .RootElement;

    [Fact]
    public void RandomMatchesCPythonRandom()
    {
        foreach (JsonElement item in Golden.GetProperty("rng").EnumerateArray())
        {
            BigInteger seed = BigInteger.Parse(item.GetProperty("seed").GetRawText());

            PythonRandom rng = new PythonRandom(seed);
            foreach (JsonElement expected in item.GetProperty("random").EnumerateArray())
            {
                Assert.Equal(expected.GetDouble(), rng.NextDouble());
            }

            PythonRandom expo = new PythonRandom(seed);
            foreach (JsonElement expected in item.GetProperty("expovariate_0_5").EnumerateArray())
            {
                Assert.Equal(expected.GetDouble(), expo.ExpoVariate(0.5), 12);
            }

            // Crosses the 624-word refill boundary of the Mersenne Twister twice.
            PythonRandom long1 = new PythonRandom(seed);
            double last = 0;
            for (int i = 0; i < 1300; i++)
            {
                last = long1.NextDouble();
            }

            Assert.Equal(item.GetProperty("random_1299").GetDouble(), last);
            Assert.Equal(item.GetProperty("random_1300_next").GetDouble(), long1.NextDouble());
        }
    }

    [Fact]
    public void RoundMatchesPythonRound()
    {
        int checkedCount = 0;
        foreach (JsonElement item in Golden.GetProperty("round").EnumerateArray())
        {
            double x = item[0].GetDouble();
            int digits = item[1].GetInt32();
            double expected = item[2].GetDouble();
            double actual = PyMath.Round(x, digits);

            Assert.True(
                BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
                "round(" + x.ToString("R") + ", " + digits + "): expected " + expected.ToString("R") + " but got " + actual.ToString("R"));
            checkedCount++;
        }

        Assert.True(checkedCount > 1000);
    }

    [Fact]
    public void MeanMatchesNumpyPairwiseSum()
    {
        foreach (JsonElement item in Golden.GetProperty("mean").EnumerateArray())
        {
            List<double> values = new List<double>();
            foreach (JsonElement v in item.GetProperty("values").EnumerateArray())
            {
                values.Add(v.GetDouble());
            }

            Assert.Equal(item.GetProperty("np_mean").GetDouble(), NumpyMath.Mean(values));
        }
    }

    [Fact]
    public void ReprMatchesPythonRepr()
    {
        foreach (JsonElement item in Golden.GetProperty("repr").EnumerateArray())
        {
            Assert.Equal(item[1].GetString(), PyFloat.Repr(item[0].GetDouble()));
        }
    }

    [Theory]
    [InlineData(0.125, 2, 0.12)]
    [InlineData(0.375, 2, 0.38)]
    [InlineData(2.675, 2, 2.67)]
    [InlineData(1.0005, 3, 1.0)]
    [InlineData(-0.5, 0, -0.0)]
    public void RoundUsesTheExactBinaryValueAndEvenTies(double x, int digits, double expected)
    {
        Assert.Equal(expected, PyMath.Round(x, digits));
    }

    [Fact]
    public void SumUsesCompensatedSummation()
    {
        // A naive loop gives 0.6000000000000001 here, Python 3.12's sum() gives 0.6.
        Assert.Equal(0.6, PyMath.Sum(new[] { 0.1, 0.2, 0.3 }));
    }

    [Theory]
    [InlineData("5", 5.0)]
    [InlineData(" 1_000.5 ", 1000.5)]
    [InlineData("1e3", 1000.0)]
    [InlineData("-Infinity", double.NegativeInfinity)]
    public void FloatParseAcceptsWhatPythonAccepts(string text, double expected)
    {
        double parsed;
        Assert.True(PyFloat.TryParse(text, out parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("0x10")]
    [InlineData("1__0")]
    [InlineData("5 5")]
    public void FloatParseRejectsWhatPythonRejects(string text)
    {
        double parsed;
        Assert.False(PyFloat.TryParse(text, out parsed));
    }

    [Fact]
    public void JsonTreeKeepsIntegerAndFloatApart()
    {
        object? parsed;
        Assert.True(JsonTree.TryParse("{\"a\": 3, \"b\": 3.0, \"c\": 1e2, \"d\": 123456789012345678901234567890, \"e\": true}", out parsed));

        Dictionary<string, object?> dict = Assert.IsType<Dictionary<string, object?>>(parsed);
        Assert.IsType<long>(dict["a"]);
        Assert.IsType<double>(dict["b"]);
        Assert.IsType<double>(dict["c"]);
        Assert.IsType<BigInteger>(dict["d"]);
        Assert.IsType<bool>(dict["e"]);
        Assert.False(PyTypes.IsInt(dict["e"]));
        Assert.False(PyTypes.IsInt(dict["b"]));
    }

    [Fact]
    public void PyReprMatchesPythonStr()
    {
        Dictionary<string, object?> value = new Dictionary<string, object?>();
        value["1"] = 0.5;
        value["x"] = new List<object?> { 1L, "y", null, true };
        Assert.Equal("{'1': 0.5, 'x': [1, 'y', None, True]}", PyRepr.Of(value));
        Assert.Equal("\"it's\"", PyRepr.Of("it's"));
    }
}
