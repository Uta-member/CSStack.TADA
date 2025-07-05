using System.Collections.Immutable;

namespace CSStack.TADA
{
    /// <summary>
    /// 入力値の検証を行うクラス
    /// </summary>
    public class ValidateHelper
    {
        /// <summary>
        /// バリデート処理を追加する
        /// </summary>
        /// <param name="action"></param>
        public void Add(Action action)
        {
            ValidateActions = ValidateActions.Add(action);
        }

        /// <summary>
        /// 値がnullでないことをチェックするバリデート処理を追加する
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
        /// バリデート処理を複数追加する
        /// </summary>
        /// <param name="actions"></param>
        public void AddRange(IEnumerable<Action> actions)
        {
            ValidateActions = ValidateActions.AddRange(actions);
        }

        /// <summary>
        /// 文字列の長さが指定範囲内であることをチェックするバリデート処理を追加する
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
                            message ?? $"文字列の長さが不正です。({minLength}～{maxLength}文字)");
                    }
                });
        }

        /// <summary>
        /// バリデート処理を実行し、例外があればMultiReasonExceptionを返す
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
        /// バリデート処理を実行し、例外が1つでもあればMultiReasonExceptionを投げる
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
        /// バリデート処理リスト
        /// </summary>
        public ImmutableList<Action> ValidateActions { get; private set; } = ImmutableList<Action>.Empty;
    }
}