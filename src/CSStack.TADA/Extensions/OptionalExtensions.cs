namespace CSStack.TADA
{
	/// <summary>
	/// CSStack.TADA.Optional extensions
	/// </summary>
	public static class OptionalExtensions
	{
		/// <summary>
		/// Creates a new value object of type <typeparamref name="TResult"/> from the primitive value inside an <see
		/// /// cref="Optional{TSource}"/> by calling <see cref="ISingleValueObject{TValue, TSelf}.Create"/>. Validation
		/// defined in <c>Create</c> is applied. Returns <see cref="Optional{TResult}.Empty"/> when the source is in the
		/// None state.
		/// </summary>
		/// <typeparam name="TSource">Type of the primitive value held by the optional</typeparam>
		/// <typeparam name="TResult">Value object type that implements <see cref="ISingleValueObject{TSource, TResult}"/></typeparam>
		/// <param name="sourceOptional">The optional primitive value to wrap into a value object</param>
		/// <returns>
		/// <see cref="Optional{TResult}"/> wrapping the newly created value object when the source has a value;
		/// otherwise <see cref="Optional{TResult}.Empty"/>.
		/// </returns>
		public static Optional<TResult> CreateSingleValueObject<TSource, TResult>(this Optional<TSource> sourceOptional)
			where TResult : ISingleValueObject<TSource, TResult>
		{
			return sourceOptional.Exchange(x => TResult.Create(x));
		}

		/// <summary>
		/// Projects the value inside an <see cref="Optional{TSource}"/> to a new type using the provided mapping
		/// function. Returns <see cref="Optional{TResult}.Empty"/> when the source is in the None state.
		/// </summary>
		/// <typeparam name="TSource">Type of the source value</typeparam>
		/// <typeparam name="TResult">Type of the projected result</typeparam>
		/// <param name="sourceOptional">The optional value to map</param>
		/// <param name="exchangeFunc">Function that converts the source value to <typeparamref name="TResult"/></param>
		/// <returns>
		/// <see cref="Optional{TResult}"/> wrapping the mapped value when the source has a value; otherwise <see ///
		/// cref="Optional{TResult}.Empty"/>.
		/// </returns>
		public static Optional<TResult> Exchange<TSource, TResult>(
			this Optional<TSource> sourceOptional,
			Func<TSource, TResult> exchangeFunc)
		{
			if (!sourceOptional.TryGetValue(out var source))
			{
				return Optional<TResult>.Empty;
			}

			return exchangeFunc.Invoke(source);
		}

		/// <summary>
		/// Extracts the underlying primitive value from an <see cref="Optional{TSource}"/> whose type implements <see
		/// /// cref="ISingleValueObject{TResult}"/>. Returns <see cref="Optional{TResult}.Empty"/> when the source is
		/// in the None state.
		/// </summary>
		/// <typeparam name="TSource">Value object type that implements <see cref="ISingleValueObject{TResult}"/></typeparam>
		/// <typeparam name="TResult">Type of the primitive value exposed by <see cref="ISingleValueObject{TResult}.Value"/></typeparam>
		/// <param name="sourceOptional">The optional value object to unwrap</param>
		/// <returns>
		/// <see cref="Optional{TResult}"/> wrapping the primitive value when the source has a value; otherwise <see 
		/// /// cref="Optional{TResult}.Empty"/>.
		/// </returns>
		public static Optional<TResult> ExchangeValueObjectToPrimitive<TSource, TResult>(
			this Optional<TSource> sourceOptional)
			where TSource : ISingleValueObject<TResult>
		{
			return sourceOptional.Exchange(x => x.Value);
		}

		/// <summary>
		/// Reconstructs a value object of type <typeparamref name="TResult"/> from the primitive value inside an <see
		/// /// cref="Optional{TSource}"/> by calling <see cref="ISingleValueObject{TValue, TSelf}.Reconstruct"/>.
		/// Intended for restoring persisted data from a repository without re-running creation- time validation.
		/// Returns <see cref="Optional{TResult}.Empty"/> when the source is in the None state.
		/// </summary>
		/// <typeparam name="TSource">Type of the primitive value held by the optional</typeparam>
		/// <typeparam name="TResult">Value object type that implements <see cref="ISingleValueObject{TSource, TResult}"/></typeparam>
		/// <param name="sourceOptional">The optional primitive value to reconstruct into a value object</param>
		/// <returns>
		/// <see cref="Optional{TResult}"/> wrapping the reconstructed value object when the source has a value;
		/// otherwise <see cref="Optional{TResult}.Empty"/>.
		/// </returns>
		public static Optional<TResult> ReconstructSingleValueObject<TSource, TResult>(
			this Optional<TSource> sourceOptional)
			where TResult : ISingleValueObject<TSource, TResult>
		{
			return sourceOptional.Exchange(x => TResult.Reconstruct(x));
		}
	}
}
