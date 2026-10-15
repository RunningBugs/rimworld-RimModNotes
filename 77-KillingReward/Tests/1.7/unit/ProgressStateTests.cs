using KillingReward.Core;
using Xunit;

namespace KillingReward.UnitTests
{
    public class ProgressStateTests
    {
        private static long ConstReq(long level) => 10;

        [Fact]
        public void SingleKill_BelowRequirement_NoLevelUp()
        {
            ProgressState s = new ProgressState(0, 0, 0).AddKill(ConstReq);
            Assert.Equal(0, s.Level);
            Assert.Equal(1, s.Progress);
            Assert.Equal(0, s.Pending);
        }

        [Fact]
        public void ExactFill_LevelsUpAndCarriesZero()
        {
            ProgressState s = new ProgressState(0, 9, 0).AddKill(ConstReq);
            Assert.Equal(1, s.Level);
            Assert.Equal(0, s.Progress);
            Assert.Equal(1, s.Pending);
        }

        [Fact]
        public void Rollover_KeepsExcessProgress()
        {
            ProgressState s = new ProgressState(0, 9, 0);
            for (int i = 0; i < 3; i++) s = s.AddKill(ConstReq); // 9+3=12 -> level1, progress 2
            Assert.Equal(1, s.Level);
            Assert.Equal(2, s.Progress);
            Assert.Equal(1, s.Pending);
        }

        [Fact]
        public void GrowingRequirement_MultiLevelUp()
        {
            // 需求依次为 2、3：4 杀 -> 第 1 级用 2（余 2），第 2 级需 3（余 2 不够），level=1 progress=2
            ProgressState s = new ProgressState(0, 0, 0);
            for (int i = 0; i < 4; i++) s = s.AddKill(l => l == 0 ? 2 : 3);
            Assert.Equal(1, s.Level);
            Assert.Equal(2, s.Progress);
            Assert.Equal(1, s.Pending);
        }

        [Fact]
        public void PendingAccumulates()
        {
            ProgressState s = new ProgressState(0, 0, 5).AddKill(l => 1);
            Assert.Equal(1, s.Level);
            Assert.Equal(6, s.Pending);
        }
    }
}
