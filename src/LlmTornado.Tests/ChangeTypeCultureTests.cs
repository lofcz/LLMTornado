using System.Globalization;
using LlmTornado.Code;

namespace LlmTornado.Tests;

[TestFixture]
public class ChangeTypeCultureTests
{
    [TestCase("2.5", 2)]
    [TestCase("2,5", 2)]
    [TestCase("1.5", 1)]
    public void ChangeType_DecimalStringToInt_IndependentOfCurrentCulture(string input, int expected)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            object? result = input.ChangeType(typeof(int));
            Assert.That(result, Is.EqualTo(expected));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
