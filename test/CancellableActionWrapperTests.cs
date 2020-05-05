using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace LargeFileFiller.UnitTest
{
    public class CancellableActionWrapperTests
    {
        [Fact]
        public async Task RunAction_Should_Ran_To_Completion_If_Not_Cancelled()
        {
            // ARRANGE
            var actionCompleted = false;

            Func<CancellationTokenSource, Task> provider = (tokenSource) =>
                Task.Factory.StartNew(() => 
                {
                    Thread.Sleep(1000);
                    tokenSource.Token.ThrowIfCancellationRequested();
                    actionCompleted = true;
                }, tokenSource.Token);

            Func<bool> cancelCondition = () => false;

            var wrapper = new CancellableActionWrapper(provider, cancelCondition);

            // ACT
            await wrapper.RunAction();

            // ASSERT
            actionCompleted.Should().BeTrue();
        }

        [Theory]
        [InlineData(1000, 50, false, "the cancellation has completed before the task completion")]
        [InlineData(50, 1000, true, "the task has completed before the cancellation")]
        public async Task RunAction_Should_Cancel_Action_Appropriately(int actionDelay, int cancelDelay, bool expectedCompletion, string message)
        {
            // ARRANGE
            var actionCompleted = false;

            Func<CancellationTokenSource, Task> provider = (tokenSource) =>
                Task.Factory.StartNew(() => 
                {
                    Thread.Sleep(actionDelay);
                    tokenSource.Token.ThrowIfCancellationRequested();
                    actionCompleted = true;
                }, tokenSource.Token);

            Func<bool> cancelCondition = () =>
            {
                Thread.Sleep(cancelDelay);
                return true;
            };

            var wrapper = new CancellableActionWrapper(provider, cancelCondition);

            // ACT
            await wrapper.RunAction();

            // ASSERT
            actionCompleted.Should().Be(expectedCompletion, message);
        }

        [Theory]
        [InlineData(false, true, "the task is not cancelled")]
        [InlineData(true, false, "the task was cancelled")]
        public async Task RunAction_Should_Raise_ActionCompleted_Event_Appropriately(bool cancelAction, bool expectedEventRaised, string message)
        {
            // ARRANGE
            var eventRaised = false;

            Func<CancellationTokenSource, Task> provider = (tokenSource) =>
                Task.Factory.StartNew(() => 
                {
                    Thread.Sleep(500);
                    tokenSource.Token.ThrowIfCancellationRequested();
                }, tokenSource.Token);

            Func<bool> cancelCondition = () => cancelAction;

            var wrapper = new CancellableActionWrapper(provider, cancelCondition);
            wrapper.ActionCompleted += delegate (object source, EventArgs e)
            {
                eventRaised = true;
            };

            // ACT
            await wrapper.RunAction();

            // ASSERT
            eventRaised.Should().Be(expectedEventRaised, message);
        }

        [Theory]
        [InlineData(false, false, "the task is not cancelled")]
        [InlineData(true, true, "the task was cancelled")]
        public async Task RunAction_Should_Raise_ActionCancelled_Event_Appropriately(bool cancelAction, bool expectedEventRaised, string message)
        {
            // ARRANGE
            var eventRaised = false;

            Func<CancellationTokenSource, Task> provider = (tokenSource) =>
                Task.Factory.StartNew(() => 
                {
                    Thread.Sleep(500);
                    tokenSource.Token.ThrowIfCancellationRequested();
                }, tokenSource.Token);

            Func<bool> cancelCondition = () => cancelAction;

            var wrapper = new CancellableActionWrapper(provider, cancelCondition);
            wrapper.ActionCancelled += delegate (object source, EventArgs e)
            {
                eventRaised = true;
            };

            // ACT
            await wrapper.RunAction();

            // ASSERT
            eventRaised.Should().Be(expectedEventRaised, message);
        }

        [Theory]
        [InlineData(false, false, "the task did not encountered an error")]
        [InlineData(true, true, "the task encountered an error")]
        public async Task RunAction_Should_Raise_ActionFaulted_Event_Appropriately(bool faultAction, bool expectedEventRaised, string message)
        {
            // ARRANGE
            var errorMessage = Guid.NewGuid().ToString();
            var eventRaised = false;
            AggregateException exception = null;

            Func<CancellationTokenSource, Task> provider = (tokenSource) =>
                Task.Factory.StartNew(() => 
                {
                    Thread.Sleep(500);
                    if (faultAction) { throw new Exception(errorMessage); }
                }, tokenSource.Token);

            Func<bool> cancelCondition = () => false;

            var wrapper = new CancellableActionWrapper(provider, cancelCondition);
            wrapper.ActionFaulted += delegate (object source, FaultedEventArgs e)
            {
                eventRaised = true;
                exception = e.Exception;
            };

            // ACT
            await wrapper.RunAction();

            // ASSERT
            eventRaised.Should().Be(expectedEventRaised, message);
            if (faultAction)
            {
                exception.Should().NotBeNull();
                exception.InnerException.Message.Should().Be(errorMessage);
            }
            else
            {
                exception.Should().BeNull();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RunAction_Should_Raise_Completed_Event_Appropriately(bool cancelAction)
        {
            // ARRANGE
            var eventRaised = false;

            Func<CancellationTokenSource, Task> provider = (tokenSource) =>
                Task.Factory.StartNew(() => 
                {
                    Thread.Sleep(500);
                    tokenSource.Token.ThrowIfCancellationRequested();
                }, tokenSource.Token);

            Func<bool> cancelCondition = () => cancelAction;

            var wrapper = new CancellableActionWrapper(provider, cancelCondition);
            wrapper.Completed += delegate (object source, EventArgs e)
            {
                eventRaised = true;
            };

            // ACT
            await wrapper.RunAction();

            // ASSERT
            eventRaised.Should().BeTrue("the event is raised always");
        }
    }
}
