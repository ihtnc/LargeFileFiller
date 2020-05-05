public class ValidateResponse
{
    private ValidateResponse() { }

    public bool Valid { get; private set; }
    public string Message { get; private set; }

    public static ValidateResponse AsInvalid(string errorMessage) =>
        new ValidateResponse { Valid = false, Message = errorMessage };

    public static ValidateResponse AsValid() =>
        new ValidateResponse { Valid = true };
}