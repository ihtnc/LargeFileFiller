using System;
using System.Threading.Tasks;

namespace LargeFileFiller
{
    public class FaultedEventArgs
    {
        private readonly Task _action;

        public AggregateException Exception => _action.Exception;

        public FaultedEventArgs(Task action)
        {
            _action = action;
        }
    }
}