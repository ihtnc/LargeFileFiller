using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LargeFileFiller
{
    public static class Helper
    {
        private const string RANDOM_STRING = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890!@#$%^&*()-=_+`~[]\\{}|;':\",./<>?\r\n\t ";
        private const string STATIC_STRING = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.\n";
        private const short BUFFER_SIZE = short.MaxValue;
        private static readonly string[] RESERVED_ARGUMENTS = new []
        {
            "--SIZE", "--UNIT", "--CONTENT", "--FILL"
        };
        private static readonly string[] RESERVED_FLAGS = new []
        {
            "--HELP", "--NOBANNER", "--SILENT", "--VERBOSE", "--APPEND"
        };

        private const int DEFAULT_SIZE = 1;
        private const SizeUnit DEFAULT_SIZE_UNIT = SizeUnit.GB;
        private const ContentFillType DEFAULT_FILL = ContentFillType.Null;

        public static async Task CreateFile(Parameters parameter, Action<decimal> updateLog, CancellationToken cancellationToken)
        {
            await Task.Factory.StartNew(() => InternalCreateFile(parameter, updateLog, cancellationToken), cancellationToken);
        }

        private static void InternalCreateFile(Parameters parameter, Action<decimal> updateLog, CancellationToken cancellationToken)
        {
            var tempFile = GetTempFileName();

            try
            {
                WriteContent(tempFile, parameter, updateLog, cancellationToken);
                File.Copy(tempFile, parameter.FileName, true);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        private static string GetTempFileName()
        {
            var tempPath = Path.GetTempPath();
            var tempFileName = $"{Guid.NewGuid()}.tmp";
            var tempFile = Path.Combine(tempPath, tempFileName);

            while (File.Exists(tempFile))
            {
                tempFileName = $"{Guid.NewGuid()}.tmp";
                tempFile = Path.Combine(tempPath, tempFileName);
            }

            return tempFile;
        }

        private static void WriteContent(string fileName, Parameters parameter, Action<decimal> updateLog, CancellationToken cancellationToken)
        {
            StreamWriter fs;

            if (parameter.AppendContent && File.Exists(parameter.FileName))
            {
                File.Copy(parameter.FileName, fileName, true);
                fs = File.AppendText(fileName);
            }
            else
            {
                fs = File.CreateText(fileName);
            }

            using (fs)
            {
                var totalSize = GetTotalSize(parameter);
                var bytesToFill = totalSize;

                while (bytesToFill > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var bytesSoFar = totalSize - bytesToFill;
                    var contentSize = (bytesToFill > BUFFER_SIZE) ? BUFFER_SIZE : (int)(bytesToFill);
                    var contents = GetString(parameter, bytesSoFar, contentSize);
                    fs.Write(contents);
                    bytesToFill -= contents.Length;
                    var progress = (decimal) bytesSoFar / totalSize;
                    updateLog(progress);
                }
            }
        }

        private static long GetTotalSize(Parameters parameter)
        {
            var totalSize = parameter.FileSize;
            var sizeUnit = parameter.FileSizeUnit;
            while (sizeUnit != SizeUnit.B)
            {
                totalSize *= 1024;
                if (sizeUnit == SizeUnit.KB) { sizeUnit = SizeUnit.B; }
                if (sizeUnit == SizeUnit.MB) { sizeUnit = SizeUnit.KB; }
                if (sizeUnit == SizeUnit.GB) { sizeUnit = SizeUnit.MB; }
            }

            return totalSize;
        }

        private static string GetString(Parameters parameter, long currentSize, int additionalSize)
        {
            if (parameter.ContentFill == ContentFillType.Null)
            {
                return GetNullString(additionalSize);
            }

            var content = parameter.ContentTemplate;
            var random = new Random(Guid.NewGuid().GetHashCode());
            var sb = new StringBuilder(additionalSize);

            for (var i = 0; i < additionalSize; i++)
            {
                var start = (parameter.ContentFill == ContentFillType.Fixed) ? currentSize + i : random.Next();
                var charIndex = (int)(start % content.Length);
                sb.Append(content[charIndex]);
            }

            return sb.ToString();
        }

        private static string GetNullString(int size) => new String('\0', size);

        public static Parameters ParseArguments(params string[] args)
        {
            var parameters = new Parameters().ApplyHelpFlag(args);
            if (parameters.ShowHelp)
            {
                return parameters;
            }

            return parameters
                .ApplyArguments(args)
                .ApplyArgumentRules();
        }

        private static Parameters ApplyHelpFlag(this Parameters parameters, params string[] args)
        {
            if (args?.Any() != true) { return parameters; }

            // help flag should always be passed as the first argument if to be used
            var helpFlag = args?.FirstOrDefault();
            parameters.ShowHelp = string.Equals(helpFlag, "--HELP", StringComparison.OrdinalIgnoreCase);

            return parameters;
        }

        private static Parameters ApplyArguments(this Parameters parameters, params string[] args)
        {
            if (args?.Any() != true) { return parameters; }

            // file name should always be passed as the first argument
            var fileName = args.FirstOrDefault();
            parameters.ApplyFileName(fileName);

            var actualArgs = args.Skip(1);
            foreach (var arg in actualArgs)
            {
                if (string.IsNullOrEmpty(arg))
                {
                    continue;
                }

                if (arg.Contains("="))
                {
                    parameters.ApplyArgument(arg);
                }
                else
                {
                    parameters.ApplyFlag(arg);
                }
            }

            return parameters;
        }

        private static Parameters ApplyFileName(this Parameters parameters, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) { return parameters; }

            var argumentParts = fileName.Split('=');
            var isPotentialArgument = argumentParts.Length == 2;
            var isArgument = isPotentialArgument && RESERVED_ARGUMENTS.Any(w => string.Equals(w, argumentParts[0], StringComparison.OrdinalIgnoreCase));

            var isFlag = RESERVED_FLAGS.Any(w => string.Equals(w, fileName, StringComparison.OrdinalIgnoreCase));

            var invalidFileName = isArgument || isFlag;
            parameters.FileName = invalidFileName || string.IsNullOrWhiteSpace(fileName) ? null : fileName;

            return parameters;
        }

        private static Parameters ApplyFlag(this Parameters parameters, string flag)
        {
            switch (flag.ToUpper())
            {
                case "--NOBANNER":
                    parameters.HideBanner = true;
                    break;

                case "--SILENT":
                    parameters.RunSilently = true;
                    break;

                case "--VERBOSE":
                    parameters.Verbose = true;
                    break;

                case "--APPEND":
                    parameters.AppendContent = true;
                    break;
            }

            return parameters;
        }

        private static Parameters ApplyArgument(this Parameters parameters, string argument)
        {
            var argParts = argument.Split('=');
            if (argParts.Length != 2) { return parameters; }

            var argName = argParts[0].Trim().ToUpper();
            var argValue = argParts[1].Trim();

            switch (argName)
            {
                case "--SIZE":
                    if (long.TryParse(argValue, out var size))
                    {
                        parameters.FileSize = size;
                    }
                    break;

                case "--UNIT":
                    if (Enum.TryParse<SizeUnit>(argValue, true, out var type))
                    {
                        parameters.FileSizeUnit = type;
                    }
                    break;

                case "--CONTENT":
                    parameters.ContentTemplate = argValue;
                    break;

                case "--FILL":
                    if (Enum.TryParse<ContentFillType>(argValue, true, out var contentFill))
                    {
                        parameters.ContentFill = contentFill;
                    }
                    break;
            }
            
            return parameters;
        }

        private static Parameters ApplyArgumentRules(this Parameters parameters)
        {
            parameters.FileSize= GetValue(() => parameters.FileSize, DEFAULT_SIZE);
            parameters.FileSizeUnit = GetValue(() => parameters.FileSizeUnit, DEFAULT_SIZE_UNIT);
            parameters.ContentFill = GetValue(() => parameters.ContentFill, DEFAULT_FILL);

            if (string.IsNullOrWhiteSpace(parameters.ContentTemplate) && parameters.ContentFill != ContentFillType.Null)
            {
                parameters.ContentTemplate =
                    parameters.ContentFill == ContentFillType.Random ? RANDOM_STRING : STATIC_STRING;
            }

            return parameters;
        }

        private static T GetValue<T>(Func<T> fieldSelector, T defaultValue)
        {
            var value = fieldSelector();
            return GetValue(fieldSelector, defaultValue, () => value.Equals(default(T)));
        }

        private static T GetValue<T>(Func<T> fieldSelector, T defaultValue, Func<bool> isDefaultCondition)
        {
            return isDefaultCondition() ? defaultValue : fieldSelector();
        }

        public static string GetHelpString()
        {
            var sb = new StringBuilder()
                .AppendDescription()
                .AppendApplicationUsage()
                .AppendArgumentDescriptions();

            return sb.ToString();
        }

        private static StringBuilder AppendDescription(this StringBuilder helpContent)
        {
            helpContent.AppendLine("Creates a file and fills it with content until the file reaches a specified size.");
            helpContent.AppendLine();
            return helpContent;
        }

        private static StringBuilder AppendApplicationUsage(this StringBuilder helpContent)
        {
            var appLocation = Assembly.GetExecutingAssembly().Location;
            var app = Path.GetFileNameWithoutExtension(appLocation);

            helpContent.AppendLine($"Usage: {app} --HELP");
            helpContent.AppendLine();
            helpContent.AppendLine($"Usage: {app} FileName");
            helpContent.AppendLine("\t[--SIZE=IntegerValue]");
            helpContent.AppendLine("\t[--UNIT=B|KB|MB|GB]");
            helpContent.AppendLine("\t[--CONTENT=StringValue]");
            helpContent.AppendLine("\t[--FILL=Null|Random|Fixed]");
            helpContent.AppendLine("\t[--APPEND]");
            helpContent.AppendLine("\t[--VERBOSE]");
            helpContent.AppendLine("\t[--NOBANNER]");
            helpContent.AppendLine("\t[--SILENT]");
            helpContent.AppendLine();

            return helpContent;
        }

        private static StringBuilder AppendArgumentDescriptions(this StringBuilder helpContent)
        {
            helpContent.AppendLine("Arguments:");
            helpContent.AppendLine( "\tFileName   - Required. The file to write the output to.");
            helpContent.AppendLine($"\t--SIZE     - The output file size. DEFAULT: {DEFAULT_SIZE}");
            helpContent.AppendLine($"\t--UNIT     - The unit of measure for the file size. DEFAULT: {DEFAULT_SIZE_UNIT}");
            helpContent.AppendLine( "\t--CONTENT  - The string to use for filling the contents. DEFAULT: Depends on the --FILL argument");
            helpContent.AppendLine($"\t--FILL     - The order on which the contents are written to the output file. DEFAULT: {DEFAULT_FILL}");
            helpContent.AppendLine( "\t--APPEND   - Append the contents to the file if it exists already.");
            helpContent.AppendLine( "\t--VERBOSE  - Display more information on the progress.");
            helpContent.AppendLine( "\t--NOBANNER - Hide the application banner.");
            helpContent.AppendLine( "\t--SILENT   - Terminate immediately after completion.");
            helpContent.AppendLine( "\t--HELP     - Show this message.");
            helpContent.AppendLine();
            return helpContent;
        }

        public static ValidateResponse Validate(this Parameters parameters)
        {
            var isFileNameBlank = string.IsNullOrWhiteSpace(parameters.FileName);
            if (isFileNameBlank)
            {
                return ValidateResponse.AsInvalid("FileName is required.");
            }

            var fileName = Path.GetFileName(parameters.FileName);
            var isFileNameInvalid = Path.GetInvalidFileNameChars().Any(c => fileName.Contains(c));
            var directoryName = Path.GetDirectoryName(parameters.FileName);
            var isPathInvalid = Path.GetInvalidPathChars().Any(c => directoryName.Contains(c));
            if (isFileNameInvalid || isPathInvalid)
            {
                return ValidateResponse.AsInvalid("Invalid FileName.");
            }

            var isDirectoryValid = string.IsNullOrWhiteSpace(directoryName) ? true : Directory.Exists(directoryName);
            if (!isDirectoryValid)
            {
                return ValidateResponse.AsInvalid("Directory does not exist.");
            };

            // even though Default is part of ContentFillType it is still considered an invalid value
            //   because this should have been updated when parsing the received arguments
            var isContentFillDefault = parameters.ContentFill == ContentFillType.Default;
            if (isContentFillDefault)
            {
                return ValidateResponse.AsInvalid("Invalid --FILL value.");
            }

            // ContentTemplate is required unless ContentFill is ContentFillType.Null
            var isContentTemplateBlank = string.IsNullOrWhiteSpace(parameters.ContentTemplate);
            var isContentTemplateNeeded = parameters.ContentFill != ContentFillType.Null && isContentTemplateBlank;
            if (isContentTemplateNeeded)
            {
                return ValidateResponse.AsInvalid("Invalid --CONTENT value.");
            }

            return ValidateResponse.AsValid();
        }

        public static string GetBannerString()
        {
            var app = Assembly.GetExecutingAssembly().GetName().Name;
            return $"{app}. (c) {DateTime.Now.Year} Art Amurao All Rights Reserved.\n\n";
        }

        public static string GetOperationString(Parameters parameter, bool fileExists)
        {
            var operation = "Writing to";
            if (fileExists)
            {
                operation = (parameter.AppendContent) ? "Appending to" : "Overwriting";
            }

            var size = (parameter.Verbose) ? $" [{parameter.FileSize} {parameter.FileSizeUnit}]" : string.Empty;
            return $"{operation} {parameter.FileName}{size}...";
        }
    }
}
