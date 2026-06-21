namespace CSStack.TADA
{
	/// <summary>
	/// Extension class for Value Objects.
	/// </summary>
	public static class ValueObjectExtensions
	{
		/// <summary>
		/// Gets a value indicating whether the value is invalid.
		/// </summary>
		/// <param name="valueObject">The value object.</param>
		/// <returns>True if the value is invalid; otherwise, false.</returns>
		public static bool IsInvalidValue(this IValueObject valueObject)
		{
			try
			{
				valueObject.Validate();
				return false;
			}
			catch
			{
				return true;
			}
		}

#if NET10_0_OR_GREATER
		extension(IValueObject valueObject)
		{
			/// <summary>
			/// Gets a value indicating whether the value is invalid.
			/// </summary>
			public bool IsInvalidValue
			{
				get
				{
					try
					{
						valueObject.Validate();
						return false;
					}
					catch
					{
						return true;
					}
				}
			}
		}
#endif
	}
}