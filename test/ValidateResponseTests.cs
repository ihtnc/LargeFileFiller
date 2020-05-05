using Xunit;
using FluentAssertions;
using System;

namespace LargeFileFiller.UnitTest
{
    public class ValidateResponseTests
    {
        [Fact]
        public void AsInvalid_Should_Return_Correctly()
        {
            // ARRANGE
            var message = Guid.NewGuid().ToString();

            // ACT
            var response = ValidateResponse.AsInvalid(message);

            // ASSERT
            response.Valid.Should().BeFalse();
            response.Message.Should().Be(message);
        }

        [Fact]
        public void AsValid_Should_Return_Correctly()
        {
            // ACT
            var response = ValidateResponse.AsValid();

            // ASSERT
            response.Valid.Should().BeTrue();
            response.Message.Should().BeNull();
        }
    }
}
