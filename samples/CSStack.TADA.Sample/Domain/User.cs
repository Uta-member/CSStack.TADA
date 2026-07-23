namespace CSStack.TADA.Sample
{
    /// <summary>
    /// ユーザー集約のエンティティ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="EntityBase{TSelf, TIdentifier}"/> を継承すると、等価性が
    /// 「実行時型が同じ、かつ <see cref="Identifier"/> が等しい」になる。
    /// 名前を変更しても同じユーザーのままなのはこのため。
    /// </para>
    /// <para>
    /// エンティティに <c>Create</c> / <c>Reconstruct</c> は強制されていないが、
    /// 値オブジェクトと同じ形に揃えておくと「新規作成の不変条件」と「復元」を混ぜずに済む。
    /// </para>
    /// </remarks>
    public sealed class User : EntityBase<User, UserId>
    {
        private User(UserId identifier, UserName name, bool isSuspended)
        {
            Identifier = identifier;
            Name = name;
            IsSuspended = isSuspended;
        }

        /// <summary>
        /// 識別子。生涯変わらない。
        /// </summary>
        public override UserId Identifier { get; }

        /// <summary>
        /// 利用停止中かどうか。
        /// </summary>
        public bool IsSuspended { get; private set; }

        /// <summary>
        /// ユーザー名。
        /// </summary>
        public UserName Name { get; private set; }

        /// <summary>
        /// 新規ユーザーを作る。新規作成時の不変条件はここで適用する。
        /// </summary>
        public static User Create(UserId identifier, UserName name)
        {
            // 新規ユーザーは必ず有効な状態から始まる、というのが新規作成時の不変条件。
            return new User(identifier, name, isSuspended: false);
        }

        /// <summary>
        /// 永続化されたデータから復元する。不変条件を再適用しない。
        /// </summary>
        public static User Reconstruct(UserId identifier, UserName name, bool isSuspended)
        {
            return new User(identifier, name, isSuspended);
        }

        /// <summary>
        /// 名前を変更する。
        /// </summary>
        /// <remarks>
        /// 「1 つのエンティティで完結するルール」はエンティティ自身に置く。
        /// 集約をまたぐルール（名前の重複禁止など）はドメインサービスの仕事。
        /// </remarks>
        /// <exception cref="DomainInvalidOperationException">利用停止中のユーザーだった。</exception>
        public void Rename(UserName name)
        {
            if (IsSuspended)
            {
                throw new DomainInvalidOperationException("利用停止中のユーザーは名前を変更できません。");
            }

            Name = name;
        }

        /// <summary>
        /// 利用を停止する。すでに停止済みなら何もしない（冪等）。
        /// </summary>
        public void Suspend()
        {
            IsSuspended = true;
        }
    }
}
