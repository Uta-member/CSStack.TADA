namespace CSStack.TADA.Tests
{
    /// <summary>
    /// The value-object contract, exercised through the sample <see cref="UserId"/> / <see cref="UserName"/>:
    /// validation lives in <c>Create</c>, <c>Reconstruct</c> skips it, length bounds are reachable through the
    /// <see cref="ILengthDefinedSingleValueObject"/> constraint, and equality is by value (record).
    /// </summary>
    public class ValueObjectTests
    {
        // --- Create validates --------------------------------------------------------------

        [Fact]
        public void Create_は不変条件を満たす値を受け入れる()
        {
            Assert.Equal("taro", UserName.Create("taro").Value);
        }

        [Fact]
        public void UserName_Create_はnullをValueObjectNullExceptionで弾く()
        {
            Assert.Throws<ValueObjectNullException>(() => UserName.Create(null!));
        }

        [Fact]
        public void UserName_Create_は長さ上限超過をValueObjectLengthExceptionで弾く()
        {
            var tooLong = new string('a', UserName.MaxLength + 1);

            var exception = Assert.Throws<ValueObjectLengthException>(() => UserName.Create(tooLong));

            Assert.Equal(UserName.MinLength, exception.MinLength);
            Assert.Equal(UserName.MaxLength, exception.MaxLength);
            Assert.Equal(tooLong.Length, exception.CurrentLength);
        }

        [Fact]
        public void UserName_Create_は空文字を長さ下限違反で弾く()
        {
            // MinLength is 1, so the empty string is a length violation, not a null violation.
            var exception = Assert.Throws<ValueObjectLengthException>(() => UserName.Create(string.Empty));

            Assert.Equal(0, exception.CurrentLength);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(16)]
        public void UserName_Create_は境界ちょうどの長さを受け入れる(int length)
        {
            var value = new string('a', length);

            Assert.Equal(value, UserName.Create(value).Value);
        }

        [Fact]
        public void UserId_Create_は空GUIDをValueObjectInvalidExceptionで弾く()
        {
            Assert.Throws<ValueObjectInvalidException>(() => UserId.Create(Guid.Empty));
        }

        // --- Reconstruct skips validation --------------------------------------------------

        [Fact]
        public void UserName_Reconstruct_は検証を行わず長さ超過の値でも復元する()
        {
            // A value written under older, looser rules must still be readable after the rules tighten.
            var tooLong = new string('a', UserName.MaxLength + 5);

            var name = UserName.Reconstruct(tooLong);

            Assert.Equal(tooLong, name.Value);
        }

        [Fact]
        public void UserId_Reconstruct_は空GUIDでも復元する()
        {
            Assert.Equal(Guid.Empty, UserId.Reconstruct(Guid.Empty).Value);
        }

        // --- length bounds reachable via the constraint ------------------------------------

        [Fact]
        public void 長さ境界は値を構築せずに制約経由で読める()
        {
            // A presentation layer can render maxlength from the same numbers the domain validates against.
            Assert.Equal(1, MinLengthOf<UserName>());
            Assert.Equal(16, MaxLengthOf<UserName>());
        }

        // --- record value equality ---------------------------------------------------------

        [Fact]
        public void 同じ値の値オブジェクトは等価()
        {
            Assert.Equal(UserName.Create("taro"), UserName.Create("taro"));
            Assert.True(UserName.Create("taro") == UserName.Create("taro"));
            Assert.NotEqual(UserName.Create("taro"), UserName.Create("jiro"));
        }

        [Fact]
        public void Create_と_Reconstruct_は同じ値なら等価()
        {
            Assert.Equal(UserName.Create("taro"), UserName.Reconstruct("taro"));

            var id = Guid.NewGuid();
            Assert.Equal(UserId.Create(id), UserId.Reconstruct(id));
        }

        [Fact]
        public void 等価な値オブジェクトは同じハッシュコードを返す()
        {
            Assert.Equal(UserName.Create("taro").GetHashCode(), UserName.Create("taro").GetHashCode());
        }

        private static int MaxLengthOf<T>() where T : ILengthDefinedSingleValueObject
        {
            return T.MaxLength;
        }

        private static int MinLengthOf<T>() where T : ILengthDefinedSingleValueObject
        {
            return T.MinLength;
        }
    }
}
