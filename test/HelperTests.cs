using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace LargeFileFiller.UnitTest
{
    public class HelperTests : IDisposable
    {
        private readonly string _testFile;

        public HelperTests()
        {
            _testFile = $"{Guid.NewGuid()}.txt";
        }

        public void Dispose()
        {
            if (File.Exists(_testFile))
            {
                File.Delete(_testFile);
            }
        }

        [Theory]
        [InlineData(500, SizeUnit.B, 500)]
        [InlineData(10, SizeUnit.KB, 10 * 1024)]
        [InlineData(2, SizeUnit.MB, 2 * 1024 * 1024)]
        [InlineData(1, SizeUnit.GB, 1024 * 1024 * 1024)]
        public async Task CreateFile_Should_Create_File_With_Correct_Size(int size, SizeUnit unit, long expectedSize)
        {
            // ARRANGE
            var parameters = new Parameters
            {
                FileName = _testFile,
                FileSize = size,
                FileSizeUnit = unit,
                ContentTemplate = "A"
            };

            // ACT
            await Helper.CreateFile(parameters, progress => {}, CancellationToken.None);

            // ASSERT
            using (var file = File.OpenRead(_testFile))
            {
                file.Length.Should().Be(expectedSize);
            }
        }

        [Fact]
        public async Task CreateFile_Should_Create_File_With_Correct_Content_When_Fill_Is_Null()
        {
            // ARRANGE
            var parameters = new Parameters
            {
                FileName = _testFile,
                FileSize = 10,
                FileSizeUnit = SizeUnit.B,
                ContentFill = ContentFillType.Null
            };

            // ACT
            await Helper.CreateFile(parameters, progress => {}, CancellationToken.None);

            // ASSERT
            var fileContent = File.ReadAllText(_testFile);
            foreach(var c in fileContent)
            {
                c.Should().Be('\0');
            }
        }

        [Fact]
        public async Task CreateFile_Should_Create_File_With_Correct_Content_When_Fill_Is_Random()
        {
            // ARRANGE
            var content = "ABCD";
            var parameters = new Parameters
            {
                FileName = _testFile,
                FileSize = 10,
                FileSizeUnit = SizeUnit.B,
                ContentTemplate = content,
                ContentFill = ContentFillType.Random
            };

            // ACT
            await Helper.CreateFile(parameters, progress => {}, CancellationToken.None);

            // ASSERT
            var fileContent = File.ReadAllText(_testFile);
            foreach(var c in fileContent)
            {
                content.IndexOf(c).Should().BeGreaterOrEqualTo(0);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateFile_Should_Handle_New_File_Correctly(bool appendContent)
        {
            // ARRANGE
            var content = "1234";
            var parameters = new Parameters
            {
                FileName = _testFile,
                FileSize = 8,
                FileSizeUnit = SizeUnit.B,
                AppendContent = appendContent,
                ContentTemplate = content,
                ContentFill = ContentFillType.Fixed
            };
            var expected = "12341234";

            // ACT
            await Helper.CreateFile(parameters, progress => {}, CancellationToken.None);

            // ASSERT
            var fileContent = File.ReadAllText(_testFile);
            fileContent.Should().Be(expected);
        }

        [Theory]
        [InlineData(true, "1234ABABAB")]
        [InlineData(false, "ABABAB")]
        public async Task CreateFile_Should_Handle_Existing_File_Correctly(bool appendContent, string expectedContent)
        {
            // ARRANGE
            File.WriteAllText(_testFile, "1234");

            var content = "AB";
            var parameters = new Parameters
            {
                FileName = _testFile,
                FileSize = 6,
                FileSizeUnit = SizeUnit.B,
                AppendContent = appendContent,
                ContentTemplate = content,
                ContentFill = ContentFillType.Fixed
            };

            // ACT
            await Helper.CreateFile(parameters, progress => {}, CancellationToken.None);

            // ASSERT
            var fileContent = File.ReadAllText(_testFile);
            fileContent.Should().Be(expectedContent);
        }

        [Theory]
        [InlineData(987)]
        [InlineData(short.MaxValue * 2)]
        [InlineData(short.MaxValue + 16574)]
        public async Task CreateFile_Should_Call_UpdateLog(long fileSize)
        {
            // current buffer size for writing is short.MaxValue so updateLog will be called for each multiple of the buffer size

            // ARRANGE
            // calculate the expected progress for each call based on the fileSize
            var calculatedProgress = new List<decimal>();
            var sizeLeft = fileSize;
            while(sizeLeft > 0)
            {
                var sizeSoFar = fileSize - sizeLeft;
                calculatedProgress.Add((decimal) sizeSoFar/fileSize);
                var buffer = sizeLeft < short.MaxValue ? sizeLeft : short.MaxValue;
                sizeLeft -= buffer;
            }
            var expectedProgress = calculatedProgress.ToArray();

            var parameters = new Parameters
            {
                FileName = _testFile,
                FileSize = fileSize,
                FileSizeUnit = SizeUnit.B,
                ContentTemplate = "a",
                ContentFill = ContentFillType.Random
            };

            // updateLog will collect the actual progress on each call
            var updateLogCalls = new List<decimal>();
            Action<decimal> updateLog = progress =>
            {
                updateLogCalls.Add(progress);
            };

            // ACT
            await Helper.CreateFile(parameters, updateLog, CancellationToken.None);

            // ASSERT
            updateLogCalls.Should().HaveSameCount(expectedProgress);
            updateLogCalls.Should().ContainInOrder(expectedProgress);
        }

        [Fact]
        public void CreateFile_Should_Handle_Cancellation()
        {
            // ARRANGE
            var parameters = new Parameters
            {
                FileName = _testFile,
                FileSize = 10,
                FileSizeUnit = SizeUnit.MB,
                ContentTemplate = "a",
                ContentFill = ContentFillType.Random
            };

            var cancelTask = new CancellationTokenSource();

            // ACT
            Action action = () =>
            {
                var task = Helper.CreateFile(parameters, progress => {}, cancelTask.Token);
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(50);
                    cancelTask.Cancel();
                });
                task.Wait();
            };

            // ASSERT
            action.Should().Throw<OperationCanceledException>();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CreateFile_Should_Delete_New_File_When_Cancelled(bool appendContent)
        {
            // ARRANGE
            var parameters = new Parameters
            {
                FileName = _testFile,
                FileSize = 10,
                FileSizeUnit = SizeUnit.MB,
                AppendContent = appendContent,
                ContentTemplate = "a",
                ContentFill = ContentFillType.Random
            };

            var cancelTask = new CancellationTokenSource();

            // ACT
            try
            {
                var task = Helper.CreateFile(parameters, progress => {}, cancelTask.Token);
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(50);
                    cancelTask.Cancel();
                });
                task.Wait();
            }
            catch(AggregateException)
            {

            }

            // ASSERT
            File.Exists(_testFile).Should().BeFalse();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CreateFile_Should_Not_Modify_Existing_File_When_Cancelled(bool appendContent)
        {
            // ARRANGE
            var expectedContent = "1234";
            File.WriteAllText(_testFile, expectedContent);

            var parameters = new Parameters
            {
                FileName = _testFile,
                FileSize = 10,
                FileSizeUnit = SizeUnit.MB,
                AppendContent = appendContent,
                ContentTemplate = "a",
                ContentFill = ContentFillType.Random
            };

            var cancelTask = new CancellationTokenSource();

            // ACT
            try
            {
                var task = Helper.CreateFile(parameters, progress => {}, cancelTask.Token);
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(50);
                    cancelTask.Cancel();
                });
                task.Wait();
            }
            catch(AggregateException)
            {

            }

            // ASSERT
            File.Exists(_testFile).Should().BeTrue();
            var fileContent = File.ReadAllText(_testFile);
            fileContent.Should().Be(expectedContent);
        }

        [Fact]
        public void ParseArguments_Should_Handle_Default_Values()
        {
            // ACT
            var parameters = Helper.ParseArguments();

            // ASSERT
            parameters.FileName.Should().BeNull();
            parameters.FileSize.Should().Be(1);
            parameters.FileSizeUnit.Should().Be(SizeUnit.GB);
            parameters.ContentFill.Should().Be(ContentFillType.Null);
            parameters.ContentTemplate.Should().BeNull();
            parameters.HideBanner.Should().BeFalse();
            parameters.RunSilently.Should().BeFalse();
            parameters.Verbose.Should().BeFalse();
            parameters.AppendContent.Should().BeFalse();
            parameters.ShowHelp.Should().BeFalse();
        }

        [Theory]
        [InlineData("name1", "name1", "value is valid")]
        [InlineData("", null, "value is invalid")]
        [InlineData("   ", null, "value is invalid")]
        [InlineData(null, null, "value is invalid")]
        public void ParseArguments_Should_Handle_FileName_Argument(string value, string expected, string message)
        {
            // ACT
            var parameters = Helper.ParseArguments($"{value}");

            // ASSERT
            parameters.FileName.Should().Be(expected, message);
        }

        [Theory]
        [InlineData("--HELP", null, "value is a flag")]
        [InlineData("--NOBANNER", null, "value is a flag")]
        [InlineData("--SILENT", null, "value is a flag")]
        [InlineData("--VERBOSE", null, "value is a flag")]
        [InlineData("--APPEND", null, "value is a flag")]
        [InlineData("--SIZE", "--SIZE", "value is not a valid flag")]
        [InlineData("--UNIT", "--UNIT", "value is not a valid flag")]
        [InlineData("--CONTENT", "--CONTENT", "value is not a valid flag")]
        [InlineData("--FILL", "--FILL", "value is not a valid flag")]
        [InlineData("--NONEXISTENTFLAG", "--NONEXISTENTFLAG", "value is not a valid flag")]
        public void ParseArguments_Should_Ignore_Reserved_Flags_As_FileName(string value, string expected, string message)
        {
            // ACT
            var parameters = Helper.ParseArguments($"{value}");

            // ASSERT
            parameters.FileName.Should().Be(expected, message);
        }

        [Theory]
        [InlineData("--SIZE=Value", null, "value is an argument")]
        [InlineData("--UNIT=Value", null, "value is an argument")]
        [InlineData("--CONTENT=Value", null, "value is an argument")]
        [InlineData("--FILL=Value", null, "value is an argument")]
        [InlineData("--HELP=Value", "--HELP=Value", "value is not a valid argument")]
        [InlineData("--NOBANNER=Value", "--NOBANNER=Value", "value is a valid argument")]
        [InlineData("--SILENT=Value", "--SILENT=Value", "value is a valid argument")]
        [InlineData("--VERBOSE=Value", "--VERBOSE=Value", "value is a valid argument")]
        [InlineData("--APPEND=Value", "--APPEND=Value", "value is a valid argument")]
        [InlineData("--NONEXISTENTARGUMENT=Value", "--NONEXISTENTARGUMENT=Value", "value is not a valid argument")]
        [InlineData("--SIZE==Value", "--SIZE==Value", "value is not a valid argument")]
        [InlineData("--UNIT=Value=", "--UNIT=Value=", "value is not a valid argument")]
        public void ParseArguments_Should_Ignore_Reserved_Arguments_As_FileName(string value, string expected, string message)
        {
            // ACT
            var parameters = Helper.ParseArguments($"{value}");

            // ASSERT
            parameters.FileName.Should().Be(expected, message);
        }

        [Theory]
        [InlineData("--HELP")]
        [InlineData("--help")]
        [InlineData("--Help")]
        [InlineData("--HeLp")]
        public void ParseArguments_Should_Handle_Help_Flag_By_Itself(string argument)
        {
            // ACT
            var parameters = Helper.ParseArguments(argument);

            // ASSERT
            parameters.ShowHelp.Should().BeTrue();
        }

        [Fact]
        public void ParseArguments_Should_Handle_Help_Flag_As_A_First_Argument()
        {
            // ACT
            var parameters = Helper.ParseArguments("--HELP", "--NOBANNER");

            // ASSERT
            parameters.ShowHelp.Should().BeTrue();
            parameters.HideBanner.Should().BeFalse("Succeeding arguments are ignored when showing help");
        }

        [Theory]
        [InlineData("--SIZE", "10", 10, "value is valid")]
        [InlineData("--size", "20", 20, "value is valid")]
        [InlineData("--Size", "30", 30, "value is valid")]
        [InlineData("--sIZe", "40", 40, "value is valid")]
        [InlineData("--SIZE", "Invalid", 1, "value is invalid and should result in a default")]
        [InlineData("--SIZE", "1.2.3", 1, "value is invalid and should result in a default")]
        [InlineData("--SIZE", "", 1, "value is invalid and should result in a default")]
        [InlineData("--SIZE", "  ", 1, "value is invalid and should result in a default")]
        public void ParseArguments_Should_Handle_Size_Argument(string argument, string value, int expected, string message)
        {
            // ACT
            var parameters = Helper.ParseArguments("fileName", $"{argument}={value}");

            // ASSERT
            parameters.FileSize.Should().Be(expected, message);
        }

        [Theory]
        [InlineData("--UNIT", "KB", SizeUnit.KB, "value is valid")]
        [InlineData("--UNIT", "kb", SizeUnit.KB, "value is valid")]
        [InlineData("--UNIT", "Kb", SizeUnit.KB, "value is valid")]
        [InlineData("--UNIT", "kB", SizeUnit.KB, "value is valid")]
        [InlineData("--unit", "MB", SizeUnit.MB, "value is valid")]
        [InlineData("--unit", "mb", SizeUnit.MB, "value is valid")]
        [InlineData("--unit", "Mb", SizeUnit.MB, "value is valid")]
        [InlineData("--unit", "mB", SizeUnit.MB, "value is valid")]
        [InlineData("--Unit", "B", SizeUnit.B, "value is valid")]
        [InlineData("--Unit", "b", SizeUnit.B, "value is valid")]
        [InlineData("--uNiT", "GB", SizeUnit.GB, "value is valid")]
        [InlineData("--uNiT", "gb", SizeUnit.GB, "value is valid")]
        [InlineData("--uNiT", "Gb", SizeUnit.GB, "value is valid")]
        [InlineData("--uNiT", "gB", SizeUnit.GB, "value is valid")]
        [InlineData("--UNIT", "Default", SizeUnit.GB, "value is invalid and should result in a default")]
        [InlineData("--UNIT", "Invalid", SizeUnit.GB, "value is invalid and should result in a default")]
        [InlineData("--UNIT", "  ", SizeUnit.GB, "value is invalid and should result in a default")]
        [InlineData("--UNIT", "", SizeUnit.GB, "value is invalid and should result in a default")]
        public void ParseArguments_Should_Handle_Unit_Argument(string argument, string value, SizeUnit expected, string message)
        {
            // ACT
            var parameters = Helper.ParseArguments("fileName", $"{argument}={value}");

            // ASSERT
            parameters.FileSizeUnit.Should().Be(expected, message);
        }

        [Theory]
        [InlineData("--CONTENT", "abcd", "abcd")]
        [InlineData("--content", "1234", "1234")]
        [InlineData("--Content", "!@#$", "!@#$")]
        [InlineData("--coNtENt", "ABCD", "ABCD")]
        public void ParseArguments_Should_Handle_Content_Argument(string argument, string value, string expected)
        {
            // ACT
            var parameters = Helper.ParseArguments("fileName", $"{argument}={value}");

            // ASSERT
            parameters.ContentTemplate.Should().Be(expected);
        }

        [Theory]
        [InlineData("  ", "")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void ParseArguments_Should_Handle_Content_Argument_When_Fill_Is_Null(string value, string expected)
        {
            // ARRANGE

            // ACT
            var parameters = Helper.ParseArguments("fileName", $"--CONTENT={value}", "--FILL=Null");

            // ASSERT
            parameters.ContentTemplate.Should().Be(expected);
        }

        [Theory]
        [InlineData("  ")]
        [InlineData("")]
        public void ParseArguments_Should_Handle_Invalid_Content_Argument_When_Fill_Is_Random(string value)
        {
            // ARRANGE
            var expected = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890!@#$%^&*()-=_+`~[]\\{}|;':\",./<>?\r\n\t ";

            // ACT
            var parameters = Helper.ParseArguments("fileName", $"--CONTENT={value}", "--FILL=Random");

            // ASSERT
            parameters.ContentTemplate.Should().Be(expected);
        }

        [Theory]
        [InlineData("  ")]
        [InlineData("")]
        public void ParseArguments_Should_Handle_Invalid_Content_Argument_When_Fill_Is_Fixed(string value)
        {
            // ARRANGE
            var expected = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.\n";

            // ACT
            var parameters = Helper.ParseArguments("fileName", $"--CONTENT={value}", "--FILL=Fixed");

            // ASSERT
            parameters.ContentTemplate.Should().Be(expected);
        }

        [Theory]
        [InlineData("--FILL", "Random", ContentFillType.Random, "value is valid")]
        [InlineData("--fill", "Fixed", ContentFillType.Fixed, "value is valid")]
        [InlineData("--Fill", "fIXed", ContentFillType.Fixed, "value is valid")]
        [InlineData("--fiLL", "RANDOM", ContentFillType.Random, "value is valid")]
        [InlineData("--FILL", "Default", ContentFillType.Null, "value is invalid and should result in a default")]
        [InlineData("--FILL", "Invalid", ContentFillType.Null, "value is invalid and should result in a default")]
        [InlineData("--FILL", "", ContentFillType.Null, "value is invalid and should result in a default")]
        [InlineData("--FILL", "  ", ContentFillType.Null, "value is invalid and should result in a default")]
        public void ParseArguments_Should_Handle_Fill_Argument(string argument, string value, ContentFillType expected, string message)
        {
            // ACT
            var parameters = Helper.ParseArguments("fileName", $"{argument}={value}");

            // ASSERT
            parameters.ContentFill.Should().Be(expected, message);
        }

        [Theory]
        [InlineData("--NOBANNER")]
        [InlineData("--nobanner")]
        [InlineData("--NoBanner")]
        [InlineData("--nObAnnEr")]
        public void ParseArguments_Should_Handle_NoBanner_Argument(string argument)
        {
             // ACT
            var parameters = Helper.ParseArguments("fileName", argument);

            // ASSERT
            parameters.HideBanner.Should().BeTrue();
        }

        [Theory]
        [InlineData("--SILENT")]
        [InlineData("--silent")]
        [InlineData("--Silent")]
        [InlineData("--silENT")]
        public void ParseArguments_Should_Handle_Silent_Argument(string argument)
        {
             // ACT
            var parameters = Helper.ParseArguments("fileName", argument);

            // ASSERT
            parameters.RunSilently.Should().BeTrue();
        }

        [Theory]
        [InlineData("--VERBOSE")]
        [InlineData("--verbose")]
        [InlineData("--Verbose")]
        [InlineData("--VERbose")]
        public void ParseArguments_Should_Handle_Verbose_Argument(string argument)
        {
             // ACT
            var parameters = Helper.ParseArguments("fileName", argument);

            // ASSERT
            parameters.Verbose.Should().BeTrue();
        }

        [Theory]
        [InlineData("--APPEND")]
        [InlineData("--append")]
        [InlineData("--Append")]
        [InlineData("--APpeNd")]
        public void ParseArguments_Should_Handle_Append_Argument(string argument)
        {
             // ACT
            var parameters = Helper.ParseArguments("fileName", argument);

            // ASSERT
            parameters.AppendContent.Should().BeTrue();
        }

        [Fact]
        public void GetHelpString_Should_Return_Correct_Text()
        {
            // ARRANGE
            var expected = new StringBuilder();
            expected.AppendLine("Creates a file and fills it with content until the file reaches a specified size.");
            expected.AppendLine();

            expected.AppendLine("Usage: LargeFileFiller --HELP");
            expected.AppendLine();

            expected.AppendLine("Usage: LargeFileFiller FileName");
            expected.AppendLine("\t[--SIZE=IntegerValue]");
            expected.AppendLine("\t[--UNIT=B|KB|MB|GB]");
            expected.AppendLine("\t[--CONTENT=StringValue]");
            expected.AppendLine("\t[--FILL=Null|Random|Fixed]");
            expected.AppendLine("\t[--APPEND]");
            expected.AppendLine("\t[--VERBOSE]");
            expected.AppendLine("\t[--NOBANNER]");
            expected.AppendLine("\t[--SILENT]");
            expected.AppendLine();

            expected.AppendLine("Arguments:");
            expected.AppendLine( "\tFileName   - Required. The file to write the output to.");
            expected.AppendLine($"\t--SIZE     - The output file size. DEFAULT: 1");
            expected.AppendLine($"\t--UNIT     - The unit of measure for the file size. DEFAULT: GB");
            expected.AppendLine( "\t--CONTENT  - The string to use for filling the contents. DEFAULT: Depends on the --FILL argument");
            expected.AppendLine($"\t--FILL     - The order on which the contents are written to the output file. DEFAULT: Null");
            expected.AppendLine( "\t--APPEND   - Append the contents to the file if it exists already.");
            expected.AppendLine( "\t--VERBOSE  - Display more information on the progress.");
            expected.AppendLine( "\t--NOBANNER - Hide the application banner.");
            expected.AppendLine( "\t--SILENT   - Terminate immediately after completion.");
            expected.AppendLine( "\t--HELP     - Show this message.");
            expected.AppendLine();

            // ACT
            var help = Helper.GetHelpString();

            // ASSERT
            help.Should().Be(expected.ToString());
        }

        [Theory]
        [InlineData("filename", true, null, "filename is valid")]
        [InlineData(null, false, "FileName is required.", "null filename is invalid")]
        [InlineData("", false, "FileName is required.", "blank filename is invalid")]
        [InlineData("   ", false, "FileName is required.", "filename with whitespaces only is invalid")]
        public void Validate_Should_Require_Parameter_FileName(string fileName, bool expectedValid, string expectedMessage, string message)
        {
            // ARRANGE
            var parameters = new Parameters
            {
                FileName = fileName,
                ContentFill = ContentFillType.Null
            };

            // ACT
            var actual = parameters.Validate();

            // ASSERT
            actual.Valid.Should().Be(expectedValid, message);
            actual.Message.Should().Be(expectedMessage, message);
        }

        [Fact]
        public void Validate_Should_Handle_Parameter_FileName_With_Invalid_FileName_Characters()
        {
            foreach(var c in Path.GetInvalidFileNameChars())
            {
                if (c == Path.AltDirectorySeparatorChar
                    || c == Path.DirectorySeparatorChar)
                { continue; }

                // ARRANGE
                var parameters = new Parameters
                {
                    FileName = $"file{c}Name"
                };

                // ACT
                var actual = parameters.Validate();

                // ASSERT
                actual.Valid.Should().BeFalse();
                actual.Message.Should().Be("Invalid FileName.", $"value contains an invalid character '{c}'");
            }
        }

        [Fact]
        public void Validate_Should_Handle_Parameter_FileName_With_Invalid_Path_Characters()
        {
            foreach (var c in Path.GetInvalidPathChars())
            {
                // ARRANGE
                var directory = $"directory{c}Name";
                var parameters = new Parameters
                {
                    FileName = Path.Combine(directory, "fileName")
                };

                // ACT
                var actual = parameters.Validate();

                // ASSERT
                actual.Valid.Should().BeFalse();
                actual.Message.Should().Be("Invalid FileName.", $"value contains an invalid character '{c}'");
            }
        }

        [Theory]
        [InlineData("test", true, null, "directory exists")]
        [InlineData("test\\non-existent", false, "Directory does not exist.", "directory does not exist")]
        [InlineData("non-existent", false, "Directory does not exist.", "directory does not exist")]
        [InlineData("drive:", false, "Directory does not exist.", "directory does not exist")]
        public void Validate_Should_Handle_Parameter_FileName_With_Invalid_Directory(string directoryName, bool expectedValue, string expectedMessage, string message)
        {
            // ARRANGE
            var name = "test";
            Directory.CreateDirectory(name);

            var parameters = new Parameters
            {
                FileName = Path.Combine(directoryName, "fileName"),
                ContentFill = ContentFillType.Null
            };

            // ACT
            var actual = parameters.Validate();

            // ASSERT
            actual.Valid.Should().Be(expectedValue, message);
            actual.Message.Should().Be(expectedMessage, message);

            Directory.Delete(name, true);
        }

        [Theory]
        [InlineData(ContentFillType.Null, true, null, "value is valid")]
        [InlineData(ContentFillType.Random, true, null, "value is valid")]
        [InlineData(ContentFillType.Fixed, true, null, "value is valid")]
        [InlineData(ContentFillType.Default, false, "Invalid --FILL value.","Default is considered invalid")]
        public void Validate_Should_Require_Parameter_ContentFill_To_Not_Be_Default(ContentFillType contentFillType, bool expectedValid, string expectedMessage, string message)
        {
            // ARRANGE
            var parameters = new Parameters
            {
                FileName = "filename",
                ContentFill = contentFillType,
                ContentTemplate = "content"
            };

            // ACT
            var actual = parameters.Validate();

            // ASSERT
            actual.Valid.Should().Be(expectedValid, message);
            actual.Message.Should().Be(expectedMessage, message);
        }

        [Theory]
        [InlineData("template", true, null, "content template is required")]
        [InlineData("", false, "Invalid --CONTENT value.", "content template is required")]
        [InlineData("   ", false, "Invalid --CONTENT value.", "content template is required")]
        [InlineData(null, false, "Invalid --CONTENT value.", "content template is required")]
        public void Validate_Should_Require_Parameter_ContentTemplate_If_ContentFill_Is_Random(string contentTemplate, bool expectedValid, string expectedMessage, string message)
        {
            // ARRANGE
            var parameters = new Parameters
            {
                FileName = "filename",
                ContentFill = ContentFillType.Random,
                ContentTemplate = contentTemplate
            };

            // ACT
            var actual = parameters.Validate();

            // ASSERT
            actual.Valid.Should().Be(expectedValid, message);
            actual.Message.Should().Be(expectedMessage, message);
        }

        [Theory]
        [InlineData("template", true, null, "content template is required")]
        [InlineData("", false, "Invalid --CONTENT value.", "content template is required")]
        [InlineData("   ", false, "Invalid --CONTENT value.", "content template is required")]
        [InlineData(null, false, "Invalid --CONTENT value.", "content template is required")]
        public void Validate_Should_Require_Parameter_ContentTemplate_If_ContentFill_Is_Fixed(string contentTemplate, bool expectedValid, string expectedMessage, string message)
        {
            // ARRANGE
            var parameters = new Parameters
            {
                FileName = "filename",
                ContentFill = ContentFillType.Fixed,
                ContentTemplate = contentTemplate
            };

            // ACT
            var actual = parameters.Validate();

            // ASSERT
            actual.Valid.Should().Be(expectedValid, message);
            actual.Message.Should().Be(expectedMessage, message);
        }

        [Theory]
        [InlineData("template", "content template is ignored")]
        [InlineData("", "content template is ignored")]
        [InlineData("   ", "content template is ignored")]
        [InlineData(null, "content template is ignored")]
        public void Validate_Should_Ignore_Parameter_ContentTemplate_If_ContentFill_Is_Null(string contentTemplate, string message)
        {
            // ARRANGE
            var parameters = new Parameters
            {
                FileName = "filename",
                ContentFill = ContentFillType.Null,
                ContentTemplate = contentTemplate
            };

            // ACT
            var actual = parameters.Validate();

            // ASSERT
            actual.Valid.Should().BeTrue(message);
            actual.Message.Should().BeNull(message);
        }

        [Fact]
        public void GetBannerString_Should_Return_Correct_Text()
        {
            // ARRANGE
            var expected = $"LargeFileFiller. (c) {DateTime.Now.Year} Art Amurao All Rights Reserved.\n\n";

            // ACT
            var help = Helper.GetBannerString();

            // ASSERT
            help.Should().Be(expected.ToString());
        }

        [Theory]
        [InlineData(false, false, "Writing to", "writing to a new file")]
        [InlineData(true, false, "Writing to", "appending text to a new file")]
        [InlineData(true, true, "Appending to", "appending to an existing file")]
        [InlineData(false, true, "Overwriting", "overwriting an existing file")]
        public void GetOperationString_Should_Return_Correct_Text(bool appendContent, bool fileExists, string expectedOperation, string message)
        {
            // ARRANGE
            var parameter = new Parameters
            {
                AppendContent = appendContent,
                FileSize = 123,
                FileSizeUnit = SizeUnit.MB,
                FileName = Guid.NewGuid().ToString()
            };

            var expected = $"{expectedOperation} {parameter.FileName}...";

            // ACT
            var help = Helper.GetOperationString(parameter, fileExists);

            // ASSERT
            help.Should().Be(expected.ToString(), message);
        }

        [Theory]
        [InlineData(true, " [123 MB]", "verbose logging adds size")]
        [InlineData(false, "", "normal logging does not add size")]
        public void GetOperationString_Should_Handle_Verbose_Argument(bool verbose, string expectedSize, string message)
        {
            // ARRANGE
            var parameter = new Parameters
            {
                Verbose = verbose,
                FileSize = 123,
                FileSizeUnit = SizeUnit.MB,
                FileName = Guid.NewGuid().ToString()
            };

            var expected = $"Writing to {parameter.FileName}{expectedSize}...";

            // ACT
            var help = Helper.GetOperationString(parameter, false);

            // ASSERT
            help.Should().Be(expected.ToString(), message);
        }
    }
}
