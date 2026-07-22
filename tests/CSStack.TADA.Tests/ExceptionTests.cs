namespace CSStack.TADA.Tests
{
    /// <summary>
    /// <see cref="ObjectNotFoundException"/> と <see cref="ObjectAlreadyExistException"/> が
    /// 対象型と識別子を保持し、既定メッセージを生成することを固定するテスト。
    /// 既存の <c>(string?, Exception?)</c> コンストラクターとの共存（オーバーロード解決が
    /// 曖昧にならないこと）も併せて確認する。
    /// </summary>
    public class ExceptionTests
    {
        private sealed class User : EntityBase<User, string>
        {
            public User(string identifier)
            {
                Identifier = identifier;
            }

            public override string Identifier { get; }
        }

        [Fact]
        public void ObjectNotFoundException_は対象型と識別子を保持する()
        {
            var exception = new ObjectNotFoundException(typeof(User), "user-1");

            Assert.Equal(typeof(User), exception.ObjectType);
            Assert.Equal("user-1", exception.Identifier);
        }

        [Fact]
        public void ObjectNotFoundException_は識別子を含む既定メッセージを生成する()
        {
            var exception = new ObjectNotFoundException(typeof(User), "user-1");

            Assert.Contains(typeof(User).FullName!, exception.Message);
            Assert.Contains("user-1", exception.Message);
        }

        [Fact]
        public void ObjectNotFoundException_は識別子がnullでもメッセージを生成する()
        {
            var exception = new ObjectNotFoundException(typeof(User), null);

            Assert.Contains(typeof(User).FullName!, exception.Message);
            Assert.Null(exception.Identifier);
        }

        [Fact]
        public void ObjectNotFoundException_はメッセージを渡せば既定メッセージを生成しない()
        {
            var exception = new ObjectNotFoundException(typeof(User), "user-1", "見つかりません");

            Assert.Equal("見つかりません", exception.Message);
            Assert.Equal(typeof(User), exception.ObjectType);
            Assert.Equal("user-1", exception.Identifier);
        }

        [Fact]
        public void ObjectNotFoundException_は対象型がnullなら例外になる()
        {
            Assert.Throws<ArgumentNullException>(() => new ObjectNotFoundException(null!, "user-1"));
        }

        [Fact]
        public void ObjectNotFoundException_の既存コンストラクターは対象型も識別子も持たない()
        {
            var innerException = new InvalidOperationException();
            var exception = new ObjectNotFoundException("見つかりません", innerException);

            Assert.Equal("見つかりません", exception.Message);
            Assert.Same(innerException, exception.InnerException);
            Assert.Null(exception.ObjectType);
            Assert.Null(exception.Identifier);
        }

        /// <summary>
        /// 引数 2 つで両方 null の呼び出しが、<c>(Type, object?)</c> 側と曖昧にならず
        /// 従来どおり <c>(string?, Exception?)</c> に解決されること。コンパイルできる時点で
        /// 曖昧さが無いことの確認になっている。
        /// </summary>
        [Fact]
        public void ObjectNotFoundException_はnull2つでも曖昧にならない()
        {
            var exception = new ObjectNotFoundException(null, null);

            Assert.Null(exception.ObjectType);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void ObjectAlreadyExistException_は対象型と識別子を保持する()
        {
            var exception = new ObjectAlreadyExistException(typeof(User), "taken@example.com");

            Assert.Equal(typeof(User), exception.ObjectType);
            Assert.Equal("taken@example.com", exception.Identifier);
            Assert.Contains(typeof(User).FullName!, exception.Message);
            Assert.Contains("taken@example.com", exception.Message);
        }

        [Fact]
        public void ObjectAlreadyExistException_の既存コンストラクターは対象型も識別子も持たない()
        {
            var exception = new ObjectAlreadyExistException("既に存在します");

            Assert.Equal("既に存在します", exception.Message);
            Assert.Null(exception.ObjectType);
            Assert.Null(exception.Identifier);
        }

        [Fact]
        public void 例外はTADAExceptionを継承している()
        {
            Assert.IsAssignableFrom<TADAException>(new ObjectNotFoundException(typeof(User), "user-1"));
            Assert.IsAssignableFrom<TADAException>(new ObjectAlreadyExistException(typeof(User), "user-1"));
        }

        [Fact]
        public void ValueObjectLengthException_は長さの情報を保持する()
        {
            var exception = new ValueObjectLengthException(minLength: 1, maxLength: 10, currentLength: 20);

            Assert.Equal(1, exception.MinLength);
            Assert.Equal(10, exception.MaxLength);
            Assert.Equal(20, exception.CurrentLength);
            Assert.IsAssignableFrom<ValueObjectInvalidException>(exception);
        }

        /// <summary>
        /// 内部例外の引数名が <c>innerException</c> であること（かつては <c>innserException</c> という
        /// 綴り誤りだった）。名前付き引数で書けることがそのまま確認になっている。
        /// </summary>
        [Fact]
        public void 内部例外の引数名はinnerExceptionである()
        {
            var innerException = new InvalidOperationException();

            Assert.Same(innerException, new TADAException(innerException: innerException).InnerException);
            Assert.Same(
                innerException,
                new DomainInvalidOperationException(innerException: innerException).InnerException);
            Assert.Same(innerException, new ObjectNotFoundException(innerException: innerException).InnerException);
            Assert.Same(
                innerException,
                new ObjectAlreadyExistException(innerException: innerException).InnerException);
            Assert.Same(
                innerException,
                new ValueObjectInvalidException(innerException: innerException).InnerException);
            Assert.Same(innerException, new ValueObjectNullException(innerException: innerException).InnerException);
            Assert.Same(
                innerException,
                new ValueObjectLengthException(1, 10, 20, innerException: innerException).InnerException);
        }
    }
}
