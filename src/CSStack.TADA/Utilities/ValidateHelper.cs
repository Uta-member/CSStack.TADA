using System.Collections.Immutable;

namespace CSStack.TADA
{
    /// <summary>
    /// Helper class for input validation.
    /// </summary>
    public class ValidateHelper
    {
        /// <summary>
        /// Add a validation action.
        /// </summary>
        /// <param name="action"></param>
        public void Add(Action action)
        {
            ValidateActions = ValidateActions.Add(action);
        }

        /// <summary>
        /// Add a validation that checks the value is not null.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="message"></param>
        /// <exception cref="ValueObjectNullException"></exception>
        public void AddNullCheck(object? value, string? message = null)
        {
            Add(
                () =>
                {
                    if(value == null)
                    {
                        throw new ValueObjectNullException(message);
                    }
                });
        }

        /// <summary>
        /// Add multiple validation actions.
        /// </summary>
        /// <param name="actions"></param>
        public void AddRange(IEnumerable<Action> actions)
        {
            ValidateActions = ValidateActions.AddRange(actions);
        }

        /// <summary>
        /// Add a validation that checks the string length is within the specified range.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="minLength"></param>
        /// <param name="maxLength"></param>
        /// <param name="message"></param>
        /// <exception cref="ValueObjectInvalidException"></exception>
        public void AddStrLengthCheck(string value, int minLength, int maxLength, string? message = null)
        {
            Add(
                () =>
                {
                    if(value.Length < minLength || value.Length > maxLength)
                    {
                        throw new ValueObjectLengthException(
                            minLength,
                            maxLength,
                            value.Length,
                            message ??
                                $"The length of the string is invalid. (Expected {minLength}–{maxLength} characters)");
                    }
                });
        }

        /// <summary>
        /// Execute validations and return MultiReasonException when any exception occurred.
        /// </summary>
        /// <returns></returns>
        public MultiReasonException? ExecuteValidate()
        {
            var exceptions = new MultiReasonException(ImmutableList<Exception>.Empty);
            foreach(var action in ValidateActions)
            {
                try
                {
                    action();
                }
				catch(Exception ex)
                {
                    exceptions.AddException(ex);
                }
            }
            if(!exceptions.Exceptions.Any())
            {
                return null;
            }
            return exceptions;
        }

        /// <summary>
        /// Execute validations and throw MultiReasonException when any exception occurred.
        /// </summary>
        public void ExecuteValidateWithThrowException()
        {
            var exceptions = new MultiReasonException(ImmutableList<Exception>.Empty);
            foreach(var action in ValidateActions)
            {
                try
                {
                    action();
                }
				catch(Exception ex)
                {
                    exceptions.AddException(ex);
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
        public ImmutableList<Action> ValidateActions { get; private set; } = ImmutableList<Action>.Empty;
    }
}