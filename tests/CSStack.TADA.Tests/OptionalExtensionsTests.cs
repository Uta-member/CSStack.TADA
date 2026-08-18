namespace CSStack.TADA.Tests
{
    /// <summary>
    /// The value-object bridging helpers on <see cref="OptionalExtensions"/>:
    /// <c>CreateSingleValueObject</c> / <c>ReconstructSingleValueObject</c> / <c>ExchangeValueObjectToPrimitive</c>.
    /// Each wraps <c>Map</c>, so all of them propagate the None state without touching the value object type.
    /// (The LINQ-shaped helpers — Map / Bind / Where / Select / SelectMany — are covered in
    /// <see cref="OptionalTests"/>.)
    /// </summary>
    public class OptionalExtensionsTests
    {
        // --- CreateSingleValueObject -------------------------------------------------------

        [Fact]
        public void CreateSingleValueObject_はSomeの素の値を検証つきで値オブジェクトにする()
        {
            var optional = Optional<string>.Some("taro").CreateSingleValueObject<string, UserName>();

            Assert.True(optional.HasValue);
            Assert.Equal(UserName.Create("taro"), optional.Value);
        }

        [Fact]
        public void CreateSingleValueObject_はNoneを伝播する()
        {
            var optional = Optional<string>.Empty.CreateSingleValueObject<string, UserName>();

            Assert.False(optional.HasValue);
        }

        [Fact]
        public void CreateSingleValueObject_は不正な値のとき_Create_の検証例外を投げる()
        {
            var tooLong = new string('a', UserName.MaxLength + 1);

            Assert.Throws<UserNameLengthException>(
                () => Optional<string>.Some(tooLong).CreateSingleValueObject<string, UserName>());
        }

        // --- ReconstructSingleValueObject --------------------------------------------------

        [Fact]
        public void ReconstructSingleValueObject_は検証せずに値オブジェクトにする()
        {
            var tooLong = new string('a', UserName.MaxLength + 1);

            var optional = Optional<string>.Some(tooLong).ReconstructSingleValueObject<string, UserName>();

            Assert.True(optional.HasValue);
            Assert.Equal(tooLong, optional.Value!.Value);
        }

        [Fact]
        public void ReconstructSingleValueObject_はNoneを伝播する()
        {
            var optional = Optional<string>.Empty.ReconstructSingleValueObject<string, UserName>();

            Assert.False(optional.HasValue);
        }

        // --- ExchangeValueObjectToPrimitive ------------------------------------------------

        [Fact]
        public void ExchangeValueObjectToPrimitive_は値オブジェクトから素の値を取り出す()
        {
            var optional = Optional<UserName>.Some(UserName.Create("taro"))
                .ExchangeValueObjectToPrimitive<UserName, string>();

            Assert.True(optional.HasValue);
            Assert.Equal("taro", optional.Value);
        }

        [Fact]
        public void ExchangeValueObjectToPrimitive_はNoneを伝播する()
        {
            var optional = Optional<UserName>.Empty.ExchangeValueObjectToPrimitive<UserName, string>();

            Assert.False(optional.HasValue);
        }

        // --- round trip --------------------------------------------------------------------

        [Fact]
        public void 素の値から値オブジェクトへ変換し戻すと元に戻る()
        {
            var roundTripped = Optional<string>.Some("taro")
                .CreateSingleValueObject<string, UserName>()
                .ExchangeValueObjectToPrimitive<UserName, string>();

            Assert.Equal(Optional<string>.Some("taro"), roundTripped);
        }
    }
}
