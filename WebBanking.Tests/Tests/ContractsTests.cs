using System.ComponentModel.DataAnnotations;
using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;
using WebBanking.Contracts.Transfers;
using Xunit;

namespace WebBanking.Tests.Tests;

public class ContractsTests
{
    [Fact]
    public void Result_Ok_SetsIsSuccessTrue_AndHoldsValue()
    {
        var result = Result<int>.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Result_Fail_SetsIsSuccessFalse_AndHoldsError()
    {
        var error = new Error(ErrorType.Validation, "Invalid input");
        var result = Result<int>.Fail(error);

        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void NonGenericResult_OkAndFail_SetIsSuccessCorrectly()
    {
        var okResult = Result.Ok();
        var failResult = Result.Fail(new Error(ErrorType.NotFound, "Not found"));

        Assert.True(okResult.IsSuccess);
        Assert.False(failResult.IsSuccess);
    }

    [Theory]
    [InlineData("", "password123")]
    [InlineData("ab", "password123")]
    [InlineData("validuser", "12345")]
    [InlineData("validuser", "")]
    public void RegisterRequest_InvalidInputs_FailValidation(string username, string password)
    {
        var dto = new RegisterRequest
        {
            Username = username,
            Password = password
        };

        var errors = ValidateModel(dto);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void RegisterRequest_ValidInputs_PassesValidation()
    {
        var dto = new RegisterRequest
        {
            Username = "theofania",
            Password = "password123"
        };

        var errors = ValidateModel(dto);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(0, "GR1601101250000000012300695", 50.0)]
    [InlineData(1, "GR1601101250000000012300695", 0.0)]
    [InlineData(1, "", 50.0)]
    public void TransferRequest_InvalidInputs_FailValidation(int fromAccountId, string toIban, decimal amount)
    {
        var dto = new TransferRequest
        {
            FromAccountId = fromAccountId,
            ToIban = toIban,
            Amount = amount
        };

        var errors = ValidateModel(dto);

        Assert.NotEmpty(errors);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
