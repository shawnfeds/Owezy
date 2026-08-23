using Owezy.Application.Auth;
using Xunit;

namespace Owezy.UnitTests.Auth;

public class OtpGeneratorTests
{
    [Fact]
    public void GenerateOtp_ProducesExactlySixNumericDigits()
    {
        var generator = new SecureOtpGenerator();

        for (int i = 0; i < 100; i++)
        {
            var otp = generator.GenerateOtp();

            Assert.NotNull(otp);
            Assert.Equal(6, otp.Length);
            Assert.True(int.TryParse(otp, out var val));
            Assert.InRange(val, 0, 999999);
        }
    }

    [Fact]
    public void GenerateOtp_PreservesLeadingZeroes()
    {
        var generator = new SecureOtpGenerator();

        for (int i = 0; i < 500; i++)
        {
            var otp = generator.GenerateOtp();
            Assert.Equal(6, otp.Length);
        }
    }
}
