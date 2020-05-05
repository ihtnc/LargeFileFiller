using System;
using System.Threading;
using System.Threading.Tasks;

namespace LargeFileFiller
{
    public interface ICancellableActionWrapper
    {
        /// <summary>
        /// An event that is raised when the action has been cancelled.
        /// </summary>
        event EventHandler ActionCancelled;

        /// <summary>
        /// An event that is raised when the action has been completed successfully.
        /// </summary>
        event EventHandler ActionCompleted;

        /// <summary>
        /// An event that is raised after the action has encountered an unhandled exception.
        /// </summary>
        event FaultedEventHandler ActionFaulted;

        /// <summary>
        /// An event that is raised after the action has either been completed.
        /// </summary>
        event EventHandler Completed;

        /// <summary>
        /// Performs the action and returns a task that gets completed by either the actual completion of the action or by cancellation.
        /// </summary>
        Task RunAction();
    }

    public class CancellableActionWrapper : ICancellableActionWrapper
    {
        public event EventHandler ActionCancelled;
        public event EventHandler ActionCompleted;
        public event FaultedEventHandler ActionFaulted;
        public event EventHandler Completed;

        private CancellationTokenSource _actionTask;
        private CancellationTokenSource _cancelTask;

        Func<CancellationTokenSource, Task> _actionProvider;
        Func<bool> _cancelCondition;

        /// <summary>
        /// A class that represents an action that can be cancelled.
        /// </summary>
        /// <param name="actionProvider">A function that returns the Task representing the action to be performed.</param>
        /// <param name="cancelCondition">Determines the condition for an action to be cancelled. Return true to indicate cancellation.</param>
        public CancellableActionWrapper(Func<CancellationTokenSource, Task> actionProvider, Func<bool> cancelCondition)
        {
            _actionProvider = actionProvider;
            _cancelCondition = cancelCondition;
        }

        public async Task RunAction()
        {
            _actionTask?.Cancel();
            _cancelTask?.Cancel();
            
            _actionTask = new CancellationTokenSource();
            _cancelTask = new CancellationTokenSource();

            // get the action and append a completion handler
            var originalAction = _actionProvider(_actionTask);
            var completionAction = originalAction
                .ContinueWith(CompleteAction, CancellationToken.None);

            // create the cancel loop and wait for it to end
            await Task.Factory.StartNew(WaitForCancellation, _cancelTask.Token);

            // ensures that the original action has indeed been completed
            await completionAction;

            OnCompleted(new EventArgs());
        }

        protected virtual void OnActionCompleted(EventArgs e)
        {
            ActionCompleted?.Invoke(this, e);
        }

        protected virtual void OnActionCancelled(EventArgs e)
        {
            ActionCancelled?.Invoke(this, e);
        }

        protected virtual void OnActionFaulted(FaultedEventArgs e)
        {
            ActionFaulted?.Invoke(this, e);
        }

        protected virtual void OnCompleted(EventArgs e)
        {
            Completed?.Invoke(this, e);
        }

        private void WaitForCancellation()
        {
            // loop until the cancel condition is met or the action has been completed
            while (!_cancelTask.IsCancellationRequested)
            {
                if (_cancelCondition())
                {
                    _actionTask.Cancel();
                    return;
                }
            }
        }

        private void CompleteAction(Task action)
        {
            if (action.IsCanceled)
            {
                OnActionCancelled(new EventArgs());
                return;
            }

            _cancelTask.Cancel();

            if (action.IsFaulted)
            {
                OnActionFaulted(new FaultedEventArgs(action));
                return;
            }

            OnActionCompleted(new EventArgs());
        }
    }
}