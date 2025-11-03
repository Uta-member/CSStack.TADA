using CSStack.TADA.MagicOnionHelper.Abstractions;
using Mapster;

namespace CSStack.TADA.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            Optional<int> optionValue = 42;
            var mpOptional = optionValue.Adapt<MPOptional<int>>();
            Assert.True(mpOptional.HasValue);
            var optionValue2 = mpOptional.Adapt<Optional<int>>();
            Assert.True(optionValue2.HasValue);
        }
    }
}