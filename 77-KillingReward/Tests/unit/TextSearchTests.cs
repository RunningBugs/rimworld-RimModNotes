using KillingReward.Core;
using Xunit;

namespace KillingReward.UnitTests
{
    public class TextSearchTests
    {
        [Fact]
        public void EmptyQuery_MatchesEverything()
        {
            Assert.True(TextSearch.Matches("玻璃钢", ""));
            Assert.True(TextSearch.Matches("玻璃钢", null));
            Assert.True(TextSearch.Matches("玻璃钢", "   "));
        }

        [Fact]
        public void Chinese_SubstringMatches()
        {
            Assert.True(TextSearch.Matches("玻璃钢", "玻璃"));
            Assert.False(TextSearch.Matches("玻璃钢", "钢铁"));
        }

        [Fact]
        public void CaseInsensitive()
        {
            Assert.True(TextSearch.Matches("Plasteel", "PLAST"));
            Assert.True(TextSearch.Matches("Plasteel", "steel"));
        }

        [Fact]
        public void NullLabel_NeverMatches()
        {
            Assert.False(TextSearch.Matches(null, "x"));
        }

        [Fact]
        public void QueryIsTrimmed()
        {
            Assert.True(TextSearch.Matches("Plasteel", "  plast  "));
        }
    }
}
