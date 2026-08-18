namespace CSStack.TADA.Tests
{
    /// <summary>
    /// The value-object contract, exercised through the sample <see cref="UserId"/> / <see cref="UserName"/>:
    /// validation lives in <c>Create</c>, <c>Reconstruct</c> skips it, length bounds are reachable as plain
    /// static members, <c>Validate</c> re-checks invariants on demand, and equality is by value (record).
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
        public void UserName_Create_はnullをUserNameInvalidExceptionで弾く()
        {
            Assert.Throws<UserNameInvalidException>(() => UserName.Create(null!));
        }

        [Fact]
        public void UserName_Create_は長さ上限超過をUserNameLengthExceptionで弾く()
        {
            var tooLong = new string('a', UserName.MaxLength + 1);

            var exception = Assert.Throws<UserNameLengthException>(() => UserName.Create(tooLong));

            Assert.Equal(UserName.MinLength, exception.MinLength);
            Assert.Equal(UserName.MaxLength, exception.MaxLength);
            Assert.Equal(tooLong.Length, exception.CurrentLength);
        }

        [Fact]
        public void UserName_Create_は空文字を長さ下限違反で弾く()
        {
            // MinLength is 1, so the empty string is a length violation, not a null violation.
            var exception = Assert.Throws<UserNameLengthException>(() => UserName.Create(string.Empty));

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
        public void UserId_Create_は空GUIDをUserIdInvalidExceptionで弾く()
        {
            Assert.Throws<UserIdInvalidException>(() => UserId.Create(Guid.Empty));
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

        // --- length bounds reachable as plain static members -------------------------------

        [Fact]
        public void 長さ境界は値を構築せずに静的メンバーとして読める()
        {
            // A presentation layer can render maxlength from the same numbers the domain validates against.
            Assert.Equal(1, UserName.MinLength);
            Assert.Equal(16, UserName.MaxLength);
        }

        // --- Validate re-checks on demand ---------------------------------------------------

        [Fact]
        public void Validateは不変条件を満たしていれば何も投げない()
        {
            UserName.Create("taro").Validate();
            UserId.New().Validate();
        }

        [Fact]
        public void ValidateはReconstructで復元した長さ違反を検出する()
        {
            var tooLong = new string('a', UserName.MaxLength + 1);
            var name = UserName.Reconstruct(tooLong);

            Assert.Throws<UserNameLengthException>(name.Validate);
        }

        [Fact]
        public void ValidateはReconstructで復元した空GUIDを検出する()
        {
            var id = UserId.Reconstruct(Guid.Empty);

            Assert.Throws<UserIdInvalidException>(id.Validate);
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
    }
}
