namespace CSStack.TADA.Tests
{
    /// <summary>
    /// <see cref="TADAException"/> のコンストラクタの基本的な振る舞いを固定するテスト。
    /// </summary>
    public class TADAExceptionTests
    {
        [Fact]
        public void コンストラクタはメッセージと内部例外を保持する()
        {
            var innerException = new InvalidOperationException();

            var exception = new TADAException("問題が発生しました", innerException);

            Assert.Equal("問題が発生しました", exception.Message);
            Assert.Same(innerException, exception.InnerException);
        }

        /// <summary>
        /// 内部例外の引数名が <c>innerException</c> であること（かつては <c>innserException</c> という
        /// 綴り誤りだった）。名前付き引数で書けることがそのまま確認になっている。
        /// </summary>
        [Fact]
        public void 内部例外の引数名はinnerExceptionである()
        {
            var innerException = new InvalidOperationException();

            var exception = new TADAException(innerException: innerException);

            Assert.Same(innerException, exception.InnerException);
        }
    }
}
