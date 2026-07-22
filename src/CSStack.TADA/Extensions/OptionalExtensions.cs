namespace CSStack.TADA
{
	/// <summary>
	/// CSStack.TADA.Optional extensions
	/// </summary>
	/// <remarks>
	/// <see cref="Select{TSource, TResult}(Optional{TSource}, Func{TSource, TResult})"/>, <see
	/// cref="SelectMany{TSource, TCollection, TResult}(Optional{TSource}, Func{TSource,
	/// Optional{TCollection}}, Func{TSource, TCollection, TResult})"/> and <see cref="Where{TSource}(Optional{TSource},
	/// Func{TSource, bool})"/> are provided so that LINQ query syntax can be used over <see cref="Optional{TValue}"/>.
	/// All of them propagate the None state without invoking the supplied delegate.
	/// </remarks>
	public static class OptionalExtensions
	{
		/// <summary>
		/// Projects the value inside an <see cref="Optional{TSource}"/> to a new <see cref="Optional{TResult}"/>
		/// using a function that itself returns an <see cref="Optional{TResult}"/>, flattening the result.
		/// Returns <see cref="Optional{TResult}.Empty"/> when the source is in the None state.
		/// </summary>
		/// <remarks>
		/// Unlike <see cref="Map{TSource, TResult}(Optional{TSource}, Func{TSource, TResult})"/> this can turn
		/// Some into None, which makes it the right choice for chaining lookups that may fail. <see
		/// cref="SelectMany{TSource, TResult}(Optional{TSource}, Func{TSource, Optional{TResult}})"/> is the
		/// same method under the LINQ name.
		/// </remarks>
		/// <typeparam name="TSource">Type of the source value</typeparam>
		/// <typeparam name="TResult">Type of the projected result</typeparam>
		/// <param name="sourceOptional">The optional value to bind</param>
		/// <param name="bindFunc">Function that converts the source value to an <see cref="Optional{TResult}"/></param>
		/// <returns>The optional returned by <paramref name="bindFunc"/>, or <see
		/// cref="Optional{TResult}.Empty"/> when the source is in the None state</returns>
		/// <exception cref="ArgumentNullException"><paramref name="bindFunc"/> is null.</exception>
		public static Optional<TResult> Bind<TSource, TResult>(
			this Optional<TSource> sourceOptional,
			Func<TSource, Optional<TResult>> bindFunc)
		{
			ArgumentNullException.ThrowIfNull(bindFunc);

			if (!sourceOptional.TryGetValue(out var source))
			{
				return Optional<TResult>.Empty;
			}

			return bindFunc.Invoke(source);
		}

		/// <summary>
		/// Creates a new value object of type <typeparamref name="TResult"/> from the primitive value inside an
		/// <see cref="Optional{TSource}"/> by calling <see cref="ISingleValueObject{TValue, TSelf}.Create"/>.
		/// Validation defined in <c>Create</c> is applied. Returns <see cref="Optional{TResult}.Empty"/> when
		/// the source is in the None state.
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
			return sourceOptional.Map(x => TResult.Create(x));
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
		/// <see cref="Optional{TResult}"/> wrapping the mapped value when the source has a value; otherwise <see
		/// cref="Optional{TResult}.Empty"/>.
		/// </returns>
		[Obsolete($"Renamed to {nameof(Map)} to match the conventional name for this operation. " +
			"Use Map (or the LINQ-compatible Select) instead.")]
		public static Optional<TResult> Exchange<TSource, TResult>(
			this Optional<TSource> sourceOptional,
			Func<TSource, TResult> exchangeFunc)
		{
			return sourceOptional.Map(exchangeFunc);
		}

		/// <summary>
		/// Extracts the underlying primitive value from an <see cref="Optional{TSource}"/> whose type implements
		/// <see cref="ISingleValueObject{TResult}"/>. Returns <see cref="Optional{TResult}.Empty"/> when the
		/// source is in the None state.
		/// </summary>
		/// <typeparam name="TSource">Value object type that implements <see cref="ISingleValueObject{TResult}"/></typeparam>
		/// <typeparam name="TResult">Type of the primitive value exposed by <see cref="ISingleValueObject{TResult}.Value"/></typeparam>
		/// <param name="sourceOptional">The optional value object to unwrap</param>
		/// <returns>
		/// <see cref="Optional{TResult}"/> wrapping the primitive value when the source has a value; otherwise
		/// <see cref="Optional{TResult}.Empty"/>.
		/// </returns>
		public static Optional<TResult> ExchangeValueObjectToPrimitive<TSource, TResult>(
			this Optional<TSource> sourceOptional)
			where TSource : ISingleValueObject<TResult>
		{
			return sourceOptional.Map(x => x.Value);
		}

		/// <summary>
		/// Projects the value inside an <see cref="Optional{TSource}"/> to a new type using the provided mapping
		/// function. Returns <see cref="Optional{TResult}.Empty"/> when the source is in the None state.
		/// </summary>
		/// <remarks>
		/// The Some state is preserved: if the source is <c>Some(value)</c> the result is <c>Some(mapped)</c>,
		/// even when <paramref name="mapFunc"/> returns null. Use <see cref="Bind{TSource,
		/// TResult}(Optional{TSource}, Func{TSource, Optional{TResult}})"/> when the projection itself may
		/// produce None. <see cref="Select{TSource, TResult}(Optional{TSource}, Func{TSource, TResult})"/> is
		/// the same method under the LINQ name.
		/// </remarks>
		/// <typeparam name="TSource">Type of the source value</typeparam>
		/// <typeparam name="TResult">Type of the projected result</typeparam>
		/// <param name="sourceOptional">The optional value to map</param>
		/// <param name="mapFunc">Function that converts the source value to <typeparamref name="TResult"/></param>
		/// <returns>
		/// <see cref="Optional{TResult}"/> wrapping the mapped value when the source has a value; otherwise <see
		/// cref="Optional{TResult}.Empty"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException"><paramref name="mapFunc"/> is null.</exception>
		public static Optional<TResult> Map<TSource, TResult>(
			this Optional<TSource> sourceOptional,
			Func<TSource, TResult> mapFunc)
		{
			ArgumentNullException.ThrowIfNull(mapFunc);

			if (!sourceOptional.TryGetValue(out var source))
			{
				return Optional<TResult>.Empty;
			}

			return mapFunc.Invoke(source);
		}

		/// <summary>
		/// Reconstructs a value object of type <typeparamref name="TResult"/> from the primitive value inside an
		/// <see cref="Optional{TSource}"/> by calling <see cref="ISingleValueObject{TValue, TSelf}.Reconstruct"/>.
		/// Intended for restoring persisted data from a repository without re-running creation-time validation.
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
			return sourceOptional.Map(x => TResult.Reconstruct(x));
		}

		/// <summary>
		/// LINQ-compatible name for <see cref="Map{TSource, TResult}(Optional{TSource}, Func{TSource, TResult})"/>.
		/// </summary>
		/// <typeparam name="TSource">Type of the source value</typeparam>
		/// <typeparam name="TResult">Type of the projected result</typeparam>
		/// <param name="sourceOptional">The optional value to map</param>
		/// <param name="selector">Function that converts the source value to <typeparamref name="TResult"/></param>
		/// <returns>
		/// <see cref="Optional{TResult}"/> wrapping the mapped value when the source has a value; otherwise <see
		/// cref="Optional{TResult}.Empty"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException"><paramref name="selector"/> is null.</exception>
		public static Optional<TResult> Select<TSource, TResult>(
			this Optional<TSource> sourceOptional,
			Func<TSource, TResult> selector)
		{
			return sourceOptional.Map(selector);
		}

		/// <summary>
		/// LINQ-compatible name for <see cref="Bind{TSource, TResult}(Optional{TSource}, Func{TSource,
		/// Optional{TResult}})"/>.
		/// </summary>
		/// <typeparam name="TSource">Type of the source value</typeparam>
		/// <typeparam name="TResult">Type of the projected result</typeparam>
		/// <param name="sourceOptional">The optional value to bind</param>
		/// <param name="selector">Function that converts the source value to an <see cref="Optional{TResult}"/></param>
		/// <returns>The optional returned by <paramref name="selector"/>, or <see
		/// cref="Optional{TResult}.Empty"/> when the source is in the None state</returns>
		/// <exception cref="ArgumentNullException"><paramref name="selector"/> is null.</exception>
		public static Optional<TResult> SelectMany<TSource, TResult>(
			this Optional<TSource> sourceOptional,
			Func<TSource, Optional<TResult>> selector)
		{
			return sourceOptional.Bind(selector);
		}

		/// <summary>
		/// Overload required by LINQ query syntax with multiple <c>from</c> clauses.
		/// </summary>
		/// <typeparam name="TSource">Type of the source value</typeparam>
		/// <typeparam name="TCollection">Type of the intermediate value produced by <paramref name="selector"/></typeparam>
		/// <typeparam name="TResult">Type of the projected result</typeparam>
		/// <param name="sourceOptional">The optional value to bind</param>
		/// <param name="selector">Function that converts the source value to an intermediate <see cref="Optional{TCollection}"/></param>
		/// <param name="resultSelector">Function that combines the source value and the intermediate value</param>
		/// <returns>
		/// <see cref="Optional{TResult}"/> wrapping the combined value when both the source and the intermediate
		/// optional have a value; otherwise <see cref="Optional{TResult}.Empty"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException"><paramref name="selector"/> or <paramref
		/// name="resultSelector"/> is null.</exception>
		public static Optional<TResult> SelectMany<TSource, TCollection, TResult>(
			this Optional<TSource> sourceOptional,
			Func<TSource, Optional<TCollection>> selector,
			Func<TSource, TCollection, TResult> resultSelector)
		{
			ArgumentNullException.ThrowIfNull(selector);
			ArgumentNullException.ThrowIfNull(resultSelector);

			return sourceOptional.Bind(
				source => selector.Invoke(source).Map(collection => resultSelector.Invoke(source, collection)));
		}

		/// <summary>
		/// Keeps the value when it satisfies <paramref name="predicate"/>; otherwise returns <see
		/// cref="Optional{TSource}.Empty"/>. A None source stays None and the predicate is not invoked.
		/// </summary>
		/// <typeparam name="TSource">Type of the source value</typeparam>
		/// <param name="sourceOptional">The optional value to filter</param>
		/// <param name="predicate">Condition the stored value must satisfy to be kept</param>
		/// <returns>The source when it has a value satisfying <paramref name="predicate"/>; otherwise <see
		/// cref="Optional{TSource}.Empty"/></returns>
		/// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
		public static Optional<TSource> Where<TSource>(
			this Optional<TSource> sourceOptional,
			Func<TSource, bool> predicate)
		{
			ArgumentNullException.ThrowIfNull(predicate);

			if (!sourceOptional.TryGetValue(out var source))
			{
				return Optional<TSource>.Empty;
			}

			return predicate.Invoke(source) ? sourceOptional : Optional<TSource>.Empty;
		}
	}
}
