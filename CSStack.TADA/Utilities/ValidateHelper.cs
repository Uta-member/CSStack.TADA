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
        /// バリデート処理を実行し、例外があればMultiReasonExceptionを返す
        /// </summary>
        /// <returns></returns>
        public MultiReasonException? ExecuteValidate()
        {
            var exception = new MultiReasonException(ImmutableList<Exception>.Empty);
            foreach(var action in ValidateActions)
            {
                try
                {
                    action();
                }
                catch(Exception ex)
                {
                    exception.AddException(ex);
                }
            }
            if(!exception.InnerExceptions.Any())
            {
                return null;
            }
            return exception;
        }

        /// <summary>
        /// バリデート処理を実行し、例外が1つでもあればMultiReasonExceptionを投げる
        /// </summary>
        public void ExecuteValidateWithThrowException()
        {
            var exception = new MultiReasonException(ImmutableList<Exception>.Empty);
            foreach(var action in ValidateActions)
            {
                try
                {
                    action();
                }
                catch(Exception ex)
                {
                    exception.AddException(ex);
                }
            }

            if(exception.InnerExceptions.Any())
            {
                throw exception;
            }
        }

        /// <summary>
        /// バリデート処理リスト
        /// </summary>
        public ImmutableList<Action> ValidateActions { get; private set; } = ImmutableList<Action>.Empty;
    }
}