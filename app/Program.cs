using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LargeFileFiller
{
    class Program
    {
        private static string _status = string.Empty;
        private static Parameters _parameter;
        private static Task<int> _operationResult;

        private static async Task<int> Main(string[] args)
        {
            _parameter = Helper.ParseArguments(args);

            if (_parameter.ShowHelp)
            {
                DisplayHelp();
                return await GetReturnTask(ReturnCodes.SUCCESS);
            }

            var validation = _parameter.Validate();
            if (!validation.Valid)
            {
                DisplayError(validation.Message);
                return await GetReturnTask(ReturnCodes.VALIDATION_ERROR);
            }

            if (!_parameter.HideBanner)
            {
                DisplayBanner();
            }

            DisplayOperation(_parameter);

            var wrapper = new CancellableActionWrapper(GetFileCreateTask, CheckCancellationKey);
            wrapper.ActionCompleted += OnActionCompleted;
            wrapper.ActionCancelled += OnActionCancelled;
            wrapper.ActionFaulted += OnActionFaulted;
            wrapper.Completed += OnCompleted;
            await wrapper.RunAction();

            return await _operationResult;
        }

        private static void OnActionCompleted(object sender, EventArgs e)
        {
            _operationResult = GetReturnTask(ReturnCodes.SUCCESS);
            UpdateStatus(_parameter, "done!");
        }

        private static void OnActionCancelled(object sender, EventArgs e)
        {
            _operationResult = GetReturnTask(ReturnCodes.CANCELLED);
            UpdateStatus(_parameter, "cancelled");
        }

        private static void OnActionFaulted(object sender, FaultedEventArgs e)
        {
            _operationResult = GetReturnTask(ReturnCodes.EXCEPTION);
            UpdateStatus(_parameter, "ERROR!\n");
            DisplayError($"{e.Exception}");
        }

        private static void OnCompleted(object sender, EventArgs e)
        {
            if (_parameter.RunSilently) { return; }

            Console.Write("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static Task GetFileCreateTask(CancellationTokenSource taskSource)
        {
            Action<decimal> updateLog = (progress) =>
            {
                if (!_parameter.Verbose || taskSource.IsCancellationRequested) { return; }
                UpdateStatus(_parameter, $"{(progress * 100):00}%");
            };

            return Helper.CreateFile(_parameter, updateLog, taskSource.Token);
        }

        private static bool CheckCancellationKey() =>
            Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape;

        private static Task<int> GetReturnTask(int errorCode) =>
            Task.FromResult(errorCode);

        private static void DisplayError(string message)
        {
            var original = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{message}");
            Console.ForegroundColor = original;
        }

        private static void DisplayHelp()
        {
            var help = Helper.GetHelpString();
            Console.Write(help);
        }

        private static void DisplayBanner()
        {
            var banner = Helper.GetBannerString();
            Console.Write(banner);
        }

        private static void DisplayOperation(Parameters parameter)
        {
            var status = Helper.GetOperationString(parameter, File.Exists(parameter.FileName));
            UpdateStatus(parameter, status);
            _status = status;
        }

        private static void UpdateStatus(Parameters parameter, string message)
        {
            Console.CursorLeft = _status.Length;
            Console.Write(message);
        }
    }
}
