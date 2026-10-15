using KillingReward.Core;
using Xunit;

namespace KillingReward.UnitTests
{
    public class ProgressCurveTests
    {
        [Fact]
        public void Exponential_LevelZero_EqualsInitial()
        {
            Assert.Equal(10L, ProgressCurve.RequiredKills(GrowthMode.Exponential, 10, 1.2, 99, 0));
        }

        [Fact]
        public void Exponential_GrowsByFactorWithRounding()
        {
            Assert.Equal(12L, ProgressCurve.RequiredKills(GrowthMode.Exponential, 10, 1.2, 99, 1));
            Assert.Equal(14L, ProgressCurve.RequiredKills(GrowthMode.Exponential, 10, 1.2, 99, 2));
            Assert.Equal(17L, ProgressCurve.RequiredKills(GrowthMode.Exponential, 10, 1.2, 99, 3));
        }

        [Fact]
        public void Linear_GrowsByIncrement()
        {
            Assert.Equal(10L, ProgressCurve.RequiredKills(GrowthMode.Linear, 10, 1.2, 5, 0));
            Assert.Equal(15L, ProgressCurve.RequiredKills(GrowthMode.Linear, 10, 1.2, 5, 1));
            Assert.Equal(60L, ProgressCurve.RequiredKills(GrowthMode.Linear, 10, 1.2, 5, 10));
        }

        [Fact]
        public void HugeLevel_DoesNotOverflow()
        {
            long v = ProgressCurve.RequiredKills(GrowthMode.Exponential, 10, 5.0, 10, 500);
            Assert.True(v > 0);
            Assert.True(v <= int.MaxValue);
        }

        [Fact]
        public void InvalidInputs_AreClamped()
        {
            Assert.Equal(1L, ProgressCurve.RequiredKills(GrowthMode.Exponential, 0, 0.5, -3, 0));
            Assert.Equal(1L, ProgressCurve.RequiredKills(GrowthMode.Linear, -5, 1.2, -3, 10));
        }
    }
}
