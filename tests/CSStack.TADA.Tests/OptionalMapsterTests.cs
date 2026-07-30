using Mapster;

namespace CSStack.TADA.Tests
{
    /// <summary>
    /// v3.0.0 で <see cref="Optional{TValue}"/> の <c>init</c> アクセサを削除し完全な不変型にした結果、
    /// Mapster の規約マッピングでは書き込めなくなったという設計判断をここで固定する。
    /// </summary>
    public class OptionalMapsterTests
    {
        /// <summary>
        /// <see cref="Optional{TValue}"/> と同じ形状（<c>HasValue</c> / <c>Value</c>）の可変プロパティを
        /// 持つマッピング元。Mapster の規約マッピングはこの形状からの書き込みを試みる。
        /// </summary>
        private sealed class MutableOptionalLike
        {
            public bool HasValue { get; set; }

            public int Value { get; set; }
        }

        /// <summary>
        /// <see cref="Optional{TValue}"/> は完全な不変型なので、既定設定では規約マッピングの
        /// 書き込み先にできない。値が黙って落ちるのではなく Mapster が例外を投げる。
        /// </summary>
        [Fact]
        public void Optionalは完全な不変型なのでMapsterの規約マッピングでは書き込めない()
        {
            var source = new MutableOptionalLike { HasValue = true, Value = 42 };

            var exception = Assert.Throws<CompileException>(() => source.Adapt<Optional<int>>());

            Assert.Contains("immutable type", exception.InnerException!.Message);
        }
    }
}
