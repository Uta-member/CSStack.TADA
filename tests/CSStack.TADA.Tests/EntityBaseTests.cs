namespace CSStack.TADA.Tests
{
    /// <summary>
    /// <see cref="EntityBase{TSelf, TIdentifier}"/> の同一性判定（識別子が等しく、かつ実行時型が同じ）を
    /// 固定するテスト。
    /// </summary>
    public class EntityBaseTests
    {
        private class User : EntityBase<User, string>
        {
            public User(string identifier, string name)
            {
                Identifier = identifier;
                Name = name;
            }

            public override string Identifier { get; }

            public string Name { get; set; }

            public override void Validate()
            {
            }
        }

        private sealed class Admin : User
        {
            public Admin(string identifier, string name)
                : base(identifier, name)
            {
            }
        }

        private sealed class Guest : User
        {
            public Guest(string identifier, string name)
                : base(identifier, name)
            {
            }
        }

        private sealed class Order : EntityBase<Order, string>
        {
            public Order(string identifier)
            {
                Identifier = identifier;
            }

            public override string Identifier { get; }

            public override void Validate()
            {
            }
        }

        // --- 識別子による同一性 -------------------------------------------------------------

        [Fact]
        public void 識別子が等しければ他の状態が違っても等価()
        {
            var left = new User("u1", "変更前");
            var right = new User("u1", "変更後");

            Assert.True(left.Equals(right));
            Assert.True(left == right);
            Assert.False(left != right);
        }

        [Fact]
        public void 識別子が違えば非等価()
        {
            var left = new User("u1", "同じ名前");
            var right = new User("u2", "同じ名前");

            Assert.False(left.Equals(right));
            Assert.False(left == right);
            Assert.True(left != right);
        }

        [Fact]
        public void 状態を変えても等価性は変わらない()
        {
            var entity = new User("u1", "変更前");
            var same = new User("u1", "変更前");

            entity.Name = "変更後";

            Assert.True(entity == same);
        }

        [Fact]
        public void 等価なら同じハッシュコードになる()
        {
            var left = new User("u1", "one");
            var right = new User("u1", "two");

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void HashSet_では識別子で同一視される()
        {
            var set = new HashSet<User> { new User("u1", "one"), new User("u1", "two"), new User("u2", "three") };

            Assert.Equal(2, set.Count);
        }

        // --- 実行時型のチェック（B-10）-----------------------------------------------------

        [Fact]
        public void 同じ基底を継承した別種のエンティティは識別子が一致しても非等価()
        {
            // 修正前は Identifier だけで比較していたため、この 2 つが等価になっていた。
            User admin = new Admin("u1", "管理者");
            User guest = new Guest("u1", "ゲスト");

            Assert.False(admin.Equals(guest));
            Assert.False(admin == guest);
            Assert.True(admin != guest);
        }

        [Fact]
        public void 派生型と基底型は識別子が一致しても非等価()
        {
            var user = new User("u1", "利用者");
            User admin = new Admin("u1", "管理者");

            Assert.False(user == admin);
            Assert.False(admin == user);
        }

        [Fact]
        public void 別のエンティティ型とは等価にならない()
        {
            var user = new User("x", "利用者");
            var order = new Order("x");

            Assert.False(user.Equals((object)order));
            Assert.False(order.Equals((object)user));
        }

        // --- 演算子の対称性（B-10）---------------------------------------------------------

        [Fact]
        public void 基底型として宣言した変数どうしでも比較できる()
        {
            // 修正前は右辺が TSelf 固定だったため、この比較はコンパイルできなかった。
            EntityBase<User, string> left = new User("u1", "one");
            EntityBase<User, string> right = new User("u1", "two");

            Assert.True(left == right);
            Assert.True(right == left);
        }

        [Fact]
        public void 同一インスタンスは等価()
        {
            var entity = new User("u1", "one");
            var alias = entity;

            Assert.True(entity.Equals(alias));
            Assert.True(entity == alias);
        }

        // --- null の扱い --------------------------------------------------------------------

        [Fact]
        public void Null_との比較は常に非等価()
        {
            var entity = new User("u1", "one");
            User? none = null;

            Assert.False(entity == none);
            Assert.False(none == entity);
            Assert.True(entity != none);
            Assert.False(entity.Equals(null));
            Assert.False(entity.Equals((object?)null));
        }

        [Fact]
        public void Null_どうしは等価()
        {
            User? left = null;
            User? right = null;

            Assert.True(left == right);
            Assert.False(left != right);
        }
    }
}
