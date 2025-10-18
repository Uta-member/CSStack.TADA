using System.Collections.Immutable;

namespace CSStack.TADA
{
    /// <summary>
    /// Helper class that provides validation actions managed by keys.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public class KeyedValidateHelper<TKey>
        where TKey : Enum
    {
        /// <summary>
        /// Add a validation action.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="action"></param>
        public void Add(TKey key, Action action)
        {
            ValidateActions = ValidateActions.Add(key, action);
        }

        /// <summary>
        /// Add a validation action for a value object.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="valueObject"></param>
        public void Add(TKey key, IValueObject valueObject)
        {
            ValidateActions = ValidateActions.Add(
                key,
                valueObject.Validate);
        }

        /// <summary>
        /// Execute validations and return KeyedMultiReasonException when any exception occurred.
        /// </summary>
        /// <returns></returns>
        public KeyedMultiReasonException<TKey>? ExecuteValidate()
        {
            var exceptions = new KeyedMultiReasonException<TKey>(ImmutableDictionary<TKey, Exception>.Empty);
            foreach(var action in ValidateActions)
            {
                try
                {
                    action.Value();
                }
				catch(Exception ex)
                {
                    exceptions.AddException(action.Key, ex);
                }
            }
            if(!exceptions.Exceptions.Any())
            {
                return null;
            }
            return exceptions;
        }

        /// <summary>
        /// Execute validations and throw KeyedMultiReasonException when any exception occurred.
        /// </summary>
        public void ExecuteValidateWithThrowException()
        {
            var exceptions = new KeyedMultiReasonException<TKey>(ImmutableDictionary<TKey, Exception>.Empty);
            foreach(var action in ValidateActions)
            {
                try
                {
                    action.Value();
                }
				catch(Exception ex)
                {
                    exceptions.AddException(action.Key, ex);
                }
            }

            if(exceptions.Exceptions.Any())
            {
                throw exceptions;
            }
        }

        /// <summary>
        /// Validation action list.
        /// </summary>
        public ImmutableDictionary<TKey, Action> ValidateActions
        {
            get;
            private set;
        } = ImmutableDictionary<TKey, Action>.Empty;
    }
}
