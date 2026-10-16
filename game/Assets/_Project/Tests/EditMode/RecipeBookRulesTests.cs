using NUnit.Framework;

namespace Cafe.Tests
{
    public class RecipeBookRulesTests
    {
        [TestCase(false, false, false, false)]
        [TestCase(true, false, false, true)]
        [TestCase(false, true, false, true)]
        [TestCase(false, false, true, true)]
        public void ShowFloating_TruthTable(bool expanded, bool tip, bool open, bool expected)
        {
            Assert.That(RecipeBookRules.ShowFloating(expanded, tip, open), Is.EqualTo(expected));
        }
    }
}
