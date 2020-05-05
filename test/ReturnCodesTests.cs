using Xunit;
using FluentAssertions;

namespace LargeFileFiller.UnitTest
{
    public class ReturnCodesTests
    {
        [Fact]
        public void Class_Should_Define_Codes()
        {
            ReturnCodes.SUCCESS.Should().Be(0);
            ReturnCodes.CANCELLED.Should().Be(1);
            ReturnCodes.VALIDATION_ERROR.Should().Be(-1);
            ReturnCodes.EXCEPTION.Should().Be(-2);
        }
    }
}
