using System.Reflection;

namespace CSStack.TADA.Tests
{
    /// <summary>
    /// <see cref="Optional{TValue}"/> の三状態（None / Some(null) / Some(value)）の振る舞いを固定するテスト。
    /// </summary>
    public class OptionalTests
    {
        private sealed class Box
        {
            public Box(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        // --- None の生成経路 ---------------------------------------------------------------

        [Fact]
        public void Empty_は値を持たない()
        {
            var optional = Optional<int>.Empty;

            Assert.False(optional.HasValue);
            Assert.Equal(0, optional.Value);
            Assert.False(optional.TryGetValue(out _));
        }

        [Fact]
        public void default_と_引数なしコンストラクタ_と_Empty_はすべて等価()
        {
            Assert.Equal(Optional<int>.Empty, default(Optional<int>));
            Assert.Equal(Optional<int>.Empty, new Optional<int>());
            Assert.True(default(Optional<int>) == new Optional<int>());
        }

        [Fact]
        public void hasValue_に_false_を渡すと値は捨てられて_None_になる()
        {
            var optional = new Optional<int>(5, hasValue: false);

            Assert.False(optional.HasValue);
            Assert.Equal(0, optional.Value);
            Assert.Equal(Optional<int>.Empty, optional);
        }

        // --- Some(null) の罠（A-4）---------------------------------------------------------

        [Fact]
        public void 暗黙変換に_null_を渡すと_None_ではなく_Some_null_になる()
        {
            // これがライブラリ最大の罠。リポジトリで `return null;` と書くとこの状態になる。
            Optional<string?> optional = null;

            Assert.True(optional.HasValue);
            Assert.Null(optional.Value);
            Assert.NotEqual(Optional<string?>.Empty, optional);
        }

        [Fact]
        public void Some_null_に対する_TryGetValue_は_true_を返し_out_は_null_になる()
        {
            var optional = Optional<string?>.Some(null);

            Assert.True(optional.TryGetValue(out var value));
            Assert.Null(value);
        }

        [Fact]
        public void None_に対する_TryGetValue_は_false_を返す()
        {
            var optional = Optional<string?>.Empty;

            Assert.False(optional.TryGetValue(out var value));
            Assert.Null(value);
        }

        // --- 値の取り出し ------------------------------------------------------------------

        [Fact]
        public void GetValue_は_None_のときだけ既定値を返す()
        {
            Assert.Equal(99, Optional<int>.Empty.GetValue(99));
            Assert.Equal(42, Optional<int>.Some(42).GetValue(99));
        }

        [Fact]
        public void GetValue_は_Some_null_のとき既定値ではなく_null_を返す()
        {
            // Some(null) は「値が設定されている」状態なので既定値にはフォールバックしない。
            Assert.Null(Optional<string?>.Some(null).GetValue("fallback"));
        }

        [Fact]
        public void GetValueOrDefault_は_GetValue_の別名()
        {
            Assert.Equal(99, Optional<int>.Empty.GetValueOrDefault(99));
            Assert.Equal(42, Optional<int>.Some(42).GetValueOrDefault(99));
        }

        // --- ToString ----------------------------------------------------------------------

        [Fact]
        public void ToString_は三状態を出し分ける()
        {
            Assert.Equal("None", Optional<string?>.Empty.ToString());
            Assert.Equal("Some(null)", Optional<string?>.Some(null).ToString());
            Assert.Equal("Some(42)", Optional<int>.Some(42).ToString());
        }

        // --- 等価性（B-2）------------------------------------------------------------------

        [Fact]
        public void None_同士は等しい()
        {
            Assert.True(Optional<string?>.Empty == Optional<string?>.Empty);
            Assert.False(Optional<string?>.Empty != Optional<string?>.Empty);
        }

        [Fact]
        public void Some_null_同士は等しい()
        {
            Assert.True(Optional<string?>.Some(null) == Optional<string?>.Some(null));
        }

        [Fact]
        public void None_と_Some_null_は等しくない()
        {
            // 三状態設計の要。ここが true になったら Optional は Nullable と区別できなくなる。
            Assert.True(Optional<string?>.Empty != Optional<string?>.Some(null));
            Assert.False(Optional<string?>.Empty.Equals(Optional<string?>.Some(null)));
        }

        [Fact]
        public void Some_同士は保持している値で比較される()
        {
            Assert.True(Optional<int>.Some(42) == Optional<int>.Some(42));
            Assert.True(Optional<int>.Some(42) != Optional<int>.Some(43));
        }

        [Fact]
        public void 参照型の等価性は_EqualityComparer_の既定に従う()
        {
            Assert.True(Optional<string>.Some("abc") == Optional<string>.Some("ab" + "c"));

            var box = new Box("a");
            Assert.True(Optional<Box>.Some(box) == Optional<Box>.Some(box));
            Assert.True(Optional<Box>.Some(new Box("a")) != Optional<Box>.Some(new Box("a")));
        }

        [Fact]
        public void 等しいインスタンスは同じハッシュコードを返す()
        {
            Assert.Equal(Optional<int>.Some(42).GetHashCode(), Optional<int>.Some(42).GetHashCode());
            Assert.Equal(Optional<int>.Empty.GetHashCode(), default(Optional<int>).GetHashCode());
            Assert.Equal(
                Optional<string?>.Some(null).GetHashCode(),
                Optional<string?>.Some(null).GetHashCode());
        }

        [Fact]
        public void None_と_Some_null_はハッシュコードも異なる()
        {
            Assert.NotEqual(Optional<string?>.Empty.GetHashCode(), Optional<string?>.Some(null).GetHashCode());
        }

        [Fact]
        public void object_版_Equals_は異なる型の相手に_false_を返す()
        {
            Assert.False(Optional<int>.Some(42).Equals((object)42));
            Assert.False(Optional<int>.Some(42).Equals(null));
            Assert.True(Optional<int>.Some(42).Equals((object)Optional<int>.Some(42)));
        }

        [Fact]
        public void 生の値との比較は暗黙変換を経由して_Some_同士の比較になる()
        {
            // Equals(42) は Equals(object) ではなく暗黙変換後の Equals(Optional<int>) に束縛される。
            Assert.True(Optional<int>.Some(42).Equals(42));
            Assert.True(Optional<int>.Some(42) == 42);
            Assert.True(Optional<int>.Empty != 42);
        }

        [Fact]
        public void HashSet_で三状態が区別される()
        {
            var set = new HashSet<Optional<string?>>
            {
                Optional<string?>.Empty,
                Optional<string?>.Some(null),
                Optional<string?>.Some("a"),
                Optional<string?>.Some("a"),
            };

            Assert.Equal(3, set.Count);
        }

        // --- 矛盾状態を作れないこと（A-5 / B-1）---------------------------------------------

        [Fact]
        public void HasValue_と_Value_にセッターが無い()
        {
            // オブジェクト初期化子で HasValue=false / Value=5 のような矛盾状態を作れないことの回帰テスト。
            Assert.Null(typeof(Optional<int>).GetProperty(nameof(Optional<int>.HasValue))!.SetMethod);
            Assert.Null(typeof(Optional<int>).GetProperty(nameof(Optional<int>.Value))!.SetMethod);
        }

        [Fact]
        public void readonly_struct_である()
        {
            var type = typeof(Optional<int>);

            Assert.True(type.IsValueType);
            Assert.Contains(
                type.GetCustomAttributes(inherit: false).OfType<Attribute>(),
                x => x.GetType().FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
        }

        [Fact]
        public void TryGetValue_に_MaybeNullWhen_属性が付いている()
        {
            var parameter = typeof(Optional<int>)
                .GetMethod(nameof(Optional<int>.TryGetValue))!
                .GetParameters()
                .Single();

            Assert.Contains(
                parameter.GetCustomAttributes(inherit: false).OfType<Attribute>(),
                x => x.GetType().Name == "MaybeNullWhenAttribute");
        }

        // --- Match -------------------------------------------------------------------------

        [Fact]
        public void Match_は状態に応じた分岐を返す()
        {
            Assert.Equal("some:42", Optional<int>.Some(42).Match(x => $"some:{x}", () => "none"));
            Assert.Equal("none", Optional<int>.Empty.Match(x => $"some:{x}", () => "none"));
        }

        [Fact]
        public void Match_は_Some_null_のとき_onSome_を呼ぶ()
        {
            var called = false;
            Optional<string?>.Some(null).Match(_ => called = true, () => called = false);

            Assert.True(called);
        }

        [Fact]
        public void Action_版_Match_は該当する側だけを呼ぶ()
        {
            var log = new List<string>();

            Optional<int>.Some(1).Match(x => log.Add($"some:{x}"), () => log.Add("none"));
            Optional<int>.Empty.Match(x => log.Add($"some:{x}"), () => log.Add("none"));

            Assert.Equal(new[] { "some:1", "none" }, log);
        }

        [Fact]
        public void Match_に_null_を渡すと例外()
        {
            Assert.Throws<ArgumentNullException>(() => Optional<int>.Some(1).Match(null!, () => 0));
            Assert.Throws<ArgumentNullException>(() => Optional<int>.Some(1).Match(x => x, null!));
        }

        // --- Map / Select ------------------------------------------------------------------

        [Fact]
        public void Map_は_Some_を射影し_None_は素通しする()
        {
            Assert.Equal(Optional<string>.Some("42"), Optional<int>.Some(42).Map(x => x.ToString()));
            Assert.Equal(Optional<string>.Empty, Optional<int>.Empty.Map(x => x.ToString()));
        }

        [Fact]
        public void Map_は_None_のときデリゲートを呼ばない()
        {
            var called = false;
            _ = Optional<int>.Empty.Map(
                x =>
                {
                    called = true;
                    return x;
                });

            Assert.False(called);
        }

        [Fact]
        public void Map_が_null_を返しても_Some_null_のまま_None_にはならない()
        {
            var mapped = Optional<int>.Some(42).Map<int, string?>(_ => null);

            Assert.True(mapped.HasValue);
            Assert.Null(mapped.Value);
        }

        [Fact]
        public void Select_は_Map_の別名()
        {
            Assert.Equal(Optional<string>.Some("42"), Optional<int>.Some(42).Select(x => x.ToString()));
        }

        [Fact]
        public void Exchange_は廃止予定だが_Map_と同じ結果を返す()
        {
#pragma warning disable CS0618 // 後方互換のための検証
            Assert.Equal(Optional<string>.Some("42"), Optional<int>.Some(42).Exchange(x => x.ToString()));
            Assert.Equal(Optional<string>.Empty, Optional<int>.Empty.Exchange(x => x.ToString()));
#pragma warning restore CS0618
        }

        // --- Bind / SelectMany / Where -----------------------------------------------------

        [Fact]
        public void Bind_は_Some_を_None_に変えられる()
        {
            Assert.Equal(Optional<int>.Empty, Optional<int>.Some(42).Bind(_ => Optional<int>.Empty));
            Assert.Equal(Optional<int>.Some(43), Optional<int>.Some(42).Bind(x => Optional<int>.Some(x + 1)));
            Assert.Equal(Optional<int>.Empty, Optional<int>.Empty.Bind(x => Optional<int>.Some(x + 1)));
        }

        [Fact]
        public void Where_は条件を満たさない値を_None_にする()
        {
            Assert.Equal(Optional<int>.Some(42), Optional<int>.Some(42).Where(x => x > 0));
            Assert.Equal(Optional<int>.Empty, Optional<int>.Some(-1).Where(x => x > 0));
            Assert.Equal(Optional<int>.Empty, Optional<int>.Empty.Where(x => x > 0));
        }

        [Fact]
        public void LINQ_クエリ構文が使える()
        {
            var result = from x in Optional<int>.Some(2)
                         from y in Optional<int>.Some(3)
                         where x < y
                         select x * y;

            Assert.Equal(Optional<int>.Some(6), result);
        }

        [Fact]
        public void LINQ_クエリ構文は途中が_None_なら_None_になる()
        {
            var result = from x in Optional<int>.Some(2)
                         from y in Optional<int>.Empty
                         select x * y;

            Assert.Equal(Optional<int>.Empty, result);
        }

        [Fact]
        public void 拡張メソッドに_null_デリゲートを渡すと例外()
        {
            Assert.Throws<ArgumentNullException>(() => Optional<int>.Some(1).Map<int, int>(null!));
            Assert.Throws<ArgumentNullException>(() => Optional<int>.Some(1).Bind<int, int>(null!));
            Assert.Throws<ArgumentNullException>(() => Optional<int>.Some(1).Where(null!));
        }
    }
}
