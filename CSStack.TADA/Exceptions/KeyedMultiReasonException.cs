using System.Collections.Immutable;

namespace CSStack.TADA
{
	/// <summary>
	/// 複数の例外をキーで管理する例外クラス
	/// </summary>
	/// <typeparam name="TKey"></typeparam>
	public class KeyedMultiReasonException<TKey> : Exception where TKey : Enum
	{
		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="innerExceptions"></param>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		public KeyedMultiReasonException(
			ImmutableDictionary<TKey, Exception> innerExceptions,
			string? message = null,
			Exception? innerException = null)
			: base(message, innerException)
		{
			Exceptions = innerExceptions;
		}

		/// <summary>
		/// 同時発生した例外をキーで管理するディクショナリ
		/// </summary>
		public ImmutableDictionary<TKey, Exception> Exceptions { get; private set; }

		/// <summary>
		/// 例外を追加する
		/// </summary>
		/// <param name="key"></param>
		/// <param name="exception"></param>
		public void AddException(TKey key, Exception exception)
		{
			Exceptions = Exceptions.Add(key, exception);
		}
	}
}
