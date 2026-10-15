using KillingReward.Core;
using Xunit;

namespace KillingReward.UnitTests
{
    public class KillEligibilityTests
    {
        [Theory]
        [InlineData(true, true, true, true)]    // 敌对派系单位被我方小人亲手击杀
        [InlineData(true, true, false, false)]  // 敌方互殴(斗蛐蛐)
        [InlineData(true, false, true, false)]  // 非敌对派系
        [InlineData(false, false, true, false)] // 无派系发狂动物
        [InlineData(false, false, false, false)]// 天灾/落石
        [InlineData(true, false, false, false)]
        [InlineData(false, true, true, false)]
        [InlineData(false, true, false, false)]
        public void TruthTable(bool hasFaction, bool hostile, bool playerPawn, bool expected)
        {
            Assert.Equal(expected, KillEligibility.ShouldCount(hasFaction, hostile, playerPawn));
        }
    }

    public class SkillMathTests
    {
        [Fact]
        public void Add3_Normal()
        {
            Assert.Equal(13, SkillMath.ClampedAdd(10, 3, 20));
        }

        [Fact]
        public void Add3_ClampsAtMax()
        {
            Assert.Equal(20, SkillMath.ClampedAdd(18, 3, 20));
            Assert.Equal(20, SkillMath.ClampedAdd(20, 3, 20));
        }

        [Fact]
        public void NeverBelowZero()
        {
            Assert.Equal(0, SkillMath.ClampedAdd(0, -5, 20));
        }
    }

    public class StackMathTests
    {
        [Fact]
        public void FullStack_IsStackLimit()
        {
            Assert.Equal(75, StackMath.FullStackCount(75));
            Assert.Equal(1, StackMath.FullStackCount(1));
        }

        [Fact]
        public void ZeroOrNegative_BecomesOne()
        {
            Assert.Equal(1, StackMath.FullStackCount(0));
        }
    }
}
