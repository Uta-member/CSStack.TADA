using System.Collections.Immutable;

namespace CSStack.TADA
{
	/// <summary>
	/// キーで管理されたバリデート処理を提供するヘルパークラス
	/// </summary>
	/// <typeparam name="TKey"></typeparam>
	public class KeyedValidateHelper<TKey> where TKey : Enum
	{
		/// <summary>
		/// バリデート処理リスト
		/// </summary>
		public ImmutableDictionary<TKey, Action> ValidateActions
		{
			get;
			private set;
		} = ImmutableDictionary<TKey, Action>.Empty;

		/// <summary>
		/// バリデート処理を追加する
		/// </summary>
		/// <param name="key"></param>
		/// <param name="action"></param>
		public void Add(TKey key, Action action)
		{
			ValidateActions = ValidateActions.Add(key, action);
		}

		/// <summary>
		/// バリデート処理を実行し、例外があればMultiReasonExceptionを返す
		/// </summary>
		/// <returns></returns>
		public KeyedMultiReasonException<TKey>? ExecuteValidate()
		{
			var exceptions = new KeyedMultiReasonException<TKey>(ImmutableDictionary<TKey, Exception>.Empty);
			foreach (var action in ValidateActions)
			{
				try
				{
					action.Value();
				}
				catch (Exception ex)
				{
					exceptions.AddException(action.Key, ex);
				}
			}
			if (!exceptions.Exceptions.Any())
			{
				return null;
			}
			return exceptions;
		}

		/// <summary>
		/// バリデート処理を実行し、例外が1つでもあればMultiReasonExceptionを投げる
		/// </summary>
		public void ExecuteValidateWithThrowException()
		{
			var exceptions = new KeyedMultiReasonException<TKey>(ImmutableDictionary<TKey, Exception>.Empty);
			foreach (var action in ValidateActions)
			{
				try
				{
					action.Value();
				}
				catch (Exception ex)
				{
					exceptions.AddException(action.Key, ex);
				}
			}

			if (exceptions.Exceptions.Any())
			{
				throw exceptions;
			}
		}
	}
}
