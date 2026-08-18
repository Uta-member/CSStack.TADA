namespace CSStack.TADA.Sample
{
    /// <summary>
    /// <see cref="IUserRepository{TSession}"/> のインメモリ実装。
    /// </summary>
    /// <remarks>
    /// <para>
    /// セッションは引数で受け取るだけで、フィールドに持たない。
    /// 複数のリポジトリが 1 つのトランザクションに参加できるのはこのため。
    /// begin / commit / rollback / dispose のどれも行わない。
    /// </para>
    /// <para>
    /// <b>具体的なセッション型 <see cref="AppSession"/> を名指しするのはここ（インフラ層）だけ。</b>
    /// 実際にストアを触るのはこのクラスなので、型引数を閉じるのは当然この層になる。
    /// ドメイン層・ユースケース層はこの型を知らないまま組み立てられており、
    /// 別のストアに載せ替えるならこのクラスとセッション型を差し替えるだけで済む。
    /// </para>
    /// </remarks>
    public sealed class InMemoryUserRepository : IUserRepository<AppSession>
    {
        /// <summary>
        /// 削除を予約する。
        /// </summary>
        /// <remarks>
        /// 既に居ないユーザーを削除しても、ここでは失敗にしない。
        /// 「居なければエラー」は呼び出し側（集約サービス / ユースケース）が決める。
        /// </remarks>
        public ValueTask DeleteAsync(
            AppSession session,
            User entity,
            OperateInfo operateInfo,
            CancellationToken cancellationToken = default)
        {
            session.StageDelete(entity.Identifier.Value);
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 識別子でユーザーを取得する。
        /// </summary>
        /// <remarks>
        /// <b>見つからないときは <c>Optional&lt;User&gt;.Empty</c> を返す。<c>return null;</c> と書かない。</b>
        /// 暗黙変換によって null は <c>Some(null)</c>（<c>HasValue = true</c>）になるため、
        /// 呼び出し側の <c>TryGetValue</c> が true を返したうえで <see cref="NullReferenceException"/> になる。
        /// 不在は正常な結果であり、<see cref="UserNotFoundException"/> を投げるのもここではない。
        /// </remarks>
        public ValueTask<Optional<User>> FindByIdentifierAsync(
            AppSession session,
            UserId identifier,
            CancellationToken cancellationToken = default)
        {
            var row = session.ReadById(identifier.Value);
            if (row is null)
            {
                return ValueTask.FromResult(Optional<User>.Empty);
            }

            return ValueTask.FromResult(Optional<User>.Some(ToEntity(row)));
        }

        /// <summary>
        /// ユーザーを保存する。<b>upsert</b> であり、既に居ても居なくても失敗しない。
        /// </summary>
        /// <remarks>
        /// 実際にデータが確定するのは <see cref="ITransactionManager"/> がコミットしたときで、
        /// このメソッドが戻った時点ではまだ確定していない。
        /// </remarks>
        public ValueTask SaveAsync(
            AppSession session,
            User entity,
            OperateInfo operateInfo,
            CancellationToken cancellationToken = default)
        {
            session.StageSave(
                new UserRow(entity.Identifier.Value, entity.Name.Value, entity.IsSuspended, operateInfo));
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 永続化された行からエンティティを組み立て直す。ここが <c>Reconstruct</c> の出番。
        /// </summary>
        private static User ToEntity(UserRow row)
        {
            return User.Reconstruct(
                UserId.Reconstruct(row.Id),
                UserName.Reconstruct(row.Name),
                row.IsSuspended);
        }
    }

    /// <summary>
    /// <see cref="IUserNameDirectory{TSession}"/> のインメモリ実装。
    /// </summary>
    /// <remarks>
    /// 集約をまたいで走査するので、リポジトリではなくこちらに置く。
    /// リポジトリと同じく、型引数を <see cref="AppSession"/> で閉じるのはこの層の仕事。
    /// </remarks>
    public sealed class InMemoryUserNameDirectory : IUserNameDirectory<AppSession>
    {
        /// <summary>
        /// 同じ名前を持つ別のユーザーが居るか。
        /// </summary>
        public ValueTask<bool> IsUsedByOtherAsync(
            AppSession session,
            UserName name,
            UserId exceptUserId,
            CancellationToken cancellationToken = default)
        {
            var isUsed = session.ReadAll()
                .Any(row => row.Id != exceptUserId.Value && row.Name == name.Value);

            return ValueTask.FromResult(isUsed);
        }
    }
}
