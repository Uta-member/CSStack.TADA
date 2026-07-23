namespace CSStack.TADA.Sample
{
    /// <summary>
    /// トランザクションセッション。TADA の <c>TSession</c> にあたるもの。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 実システムでは <c>DbContext</c> や <c>DbConnection</c> + <c>DbTransaction</c> がこれにあたる。
    /// 満たすべき条件は <see cref="IDisposable"/> であることだけ。
    /// </para>
    /// <para>
    /// このサンプルでは書き込みを <see cref="_pending"/> に溜め、<see cref="Commit"/> で初めて
    /// <see cref="AppDatabase"/> に反映する。読み取りは <see cref="_pending"/> を先に見るので、
    /// 同じトランザクション内では自分の未コミットの書き込みが見える。
    /// </para>
    /// <para>
    /// <b>Dispose を呼ぶのは <see cref="ITransactionManager"/>。</b>
    /// このクラスも <see cref="AppTransactionService"/> も自分で Dispose を呼ばない。
    /// </para>
    /// </remarks>
    public sealed class AppSession : IDisposable
    {
        private readonly AppDatabase _database;

        // 未コミットの変更。値が null なら「削除待ち」。
        private readonly Dictionary<Guid, UserRow?> _pending = new();

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public AppSession(AppDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// Dispose 済みかどうか。サンプルの出力で所有権を確認するために公開している。
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// 溜めた変更をデータベースに反映する。
        /// </summary>
        public void Commit()
        {
            foreach (var (id, row) in _pending)
            {
                if (row is null)
                {
                    _database.Remove(id);
                }
                else
                {
                    _database.Set(row);
                }
            }

            _pending.Clear();
        }

        /// <summary>
        /// 後始末。<see cref="ITransactionManager"/> だけが呼ぶ。
        /// </summary>
        public void Dispose()
        {
            IsDisposed = true;
        }

        /// <summary>
        /// コミット済みの全行に未コミットの変更を重ねて返す。クエリサービスから使う。
        /// </summary>
        public IEnumerable<UserRow> ReadAll()
        {
            var rows = _database.Users.ToDictionary(row => row.Id);
            foreach (var (id, pending) in _pending)
            {
                if (pending is null)
                {
                    rows.Remove(id);
                }
                else
                {
                    rows[id] = pending;
                }
            }

            return rows.Values;
        }

        /// <summary>
        /// 1 行読む。未コミットの変更を優先する。
        /// </summary>
        public UserRow? ReadById(Guid id)
        {
            if (_pending.TryGetValue(id, out var pending))
            {
                return pending;
            }

            return _database.TryGet(id);
        }

        /// <summary>
        /// 溜めた変更を捨てる。
        /// </summary>
        public void Rollback()
        {
            _pending.Clear();
        }

        /// <summary>
        /// 削除を予約する。
        /// </summary>
        public void StageDelete(Guid id)
        {
            _pending[id] = null;
        }

        /// <summary>
        /// 追加・更新を予約する。
        /// </summary>
        public void StageSave(UserRow row)
        {
            _pending[row.Id] = row;
        }
    }
}
