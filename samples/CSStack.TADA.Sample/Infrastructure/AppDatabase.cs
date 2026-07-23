namespace CSStack.TADA.Sample
{
    /// <summary>
    /// 永続化された 1 行。エンティティそのものではなく、素の値と最終更新の操作情報を持つ。
    /// </summary>
    /// <remarks>
    /// エンティティは読み出し時にこの行から組み立て直す（<c>Reconstruct</c>）。
    /// そうすると操作情報をエンティティに持たせずに済む。
    /// </remarks>
    public sealed record UserRow(Guid Id, string Name, bool IsSuspended, OperateInfo OperateInfo);

    /// <summary>
    /// コミット済みの状態。実システムのデータベースに相当する。
    /// </summary>
    /// <remarks>
    /// サンプルなのでプロセス内の <see cref="Dictionary{TKey, TValue}"/>。
    /// DI にはシングルトンで登録する（＝アプリの寿命と同じ「データベース」）。
    /// </remarks>
    public sealed class AppDatabase
    {
        private readonly Dictionary<Guid, UserRow> _users = new();

        /// <summary>
        /// コミット済みの全行。クエリサービスと、コミット処理から使う。
        /// </summary>
        public IReadOnlyCollection<UserRow> Users => _users.Values;

        /// <summary>
        /// 行を削除する。
        /// </summary>
        public void Remove(Guid id)
        {
            _users.Remove(id);
        }

        /// <summary>
        /// 行を追加または更新する。
        /// </summary>
        public void Set(UserRow row)
        {
            _users[row.Id] = row;
        }

        /// <summary>
        /// 行を取得する。無ければ null。
        /// </summary>
        public UserRow? TryGet(Guid id)
        {
            return _users.TryGetValue(id, out var row) ? row : null;
        }
    }
}
