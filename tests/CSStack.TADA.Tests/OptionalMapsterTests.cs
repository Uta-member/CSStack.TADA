using CSStack.TADA.MagicOnionHelper.Abstractions;
using Mapster;

namespace CSStack.TADA.Tests
{
    /// <summary>
    /// <c>CSStack.TADA.MagicOnionHelper.Abstractions</c> の <see cref="MPOptional{TValue}"/> との相互変換を検証する。
    /// v3.0.0 で <see cref="Optional{TValue}"/> の <c>init</c> アクセサを削除したため、
    /// <see cref="MPOptional{TValue}"/> → <see cref="Optional{TValue}"/> 方向の Mapster 規約マッピングは
    /// 明示的な設定なしでは動かなくなった。その事実と回避策をここで固定する。
    /// </summary>
    public class OptionalMapsterTests
    {
        /// <summary>
        /// パッケージが提供する変換メソッド。init 削除の影響を受けないため、これが推奨経路。
        /// </summary>
        [Fact]
        public void MPOptional_の_ToOptional_で変換できる()
        {
            var some = new MPOptional<int> { HasValue = true, Value = 42 }.ToOptional();
            Assert.True(some.HasValue);
            Assert.Equal(42, some.Value);

            var none = new MPOptional<int> { HasValue = false, Value = 0 }.ToOptional();
            Assert.False(none.HasValue);
        }

        /// <summary>
        /// <see cref="Optional{TValue}"/> → <see cref="MPOptional{TValue}"/> 方向。
        /// </summary>
        [Fact]
        public void Optional_の_FromOptional_と_ToMPOptional_で変換できる()
        {
            var fromOptional = MPOptional<int>.FromOptional(Optional<int>.Some(42));
            Assert.True(fromOptional.HasValue);
            Assert.Equal(42, fromOptional.Value);

            var toMpOptional = Optional<int>.Some(42).ToMPOptional();
            Assert.True(toMpOptional.HasValue);
            Assert.Equal(42, toMpOptional.Value);
        }

        /// <summary>
        /// <see cref="MPOptional{TValue}"/> はセッターを持つので、この方向は規約マッピングのまま動く。
        /// </summary>
        [Fact]
        public void Mapster_は_Optional_から_MPOptional_へ規約どおり変換できる()
        {
            Optional<int> optionValue = 42;

            var mpOptional = optionValue.Adapt<MPOptional<int>>();

            Assert.True(mpOptional.HasValue);
            Assert.Equal(42, mpOptional.Value);
        }

        /// <summary>
        /// 逆方向は <see cref="Optional{TValue}"/> が完全な不変型になったため、既定設定では失敗する。
        /// 値が黙って落ちるのではなく Mapster が例外を投げ、対処法（<c>MapWith</c>）まで示してくれる。
        /// </summary>
        [Fact]
        public void Mapster_は_MPOptional_から_Optional_へ既定設定では変換できない()
        {
            var mpOptional = new MPOptional<int> { HasValue = true, Value = 42 };

            var exception = Assert.Throws<CompileException>(() => mpOptional.Adapt<Optional<int>>());

            Assert.Contains("immutable type", exception.InnerException!.Message);
        }

        /// <summary>
        /// 回避策: パッケージの変換メソッドを <c>MapWith</c> で登録する。
        /// </summary>
        [Fact]
        public void MapWith_を登録すれば_MPOptional_から_Optional_へ変換できる()
        {
            var config = new TypeAdapterConfig();
            config.NewConfig<MPOptional<int>, Optional<int>>().MapWith(src => src.ToOptional());

            var optional = new MPOptional<int> { HasValue = true, Value = 42 }.Adapt<Optional<int>>(config);

            Assert.True(optional.HasValue);
            Assert.Equal(42, optional.Value);
        }
    }
}
