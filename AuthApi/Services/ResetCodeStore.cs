namespace AuthApi.Services;

public class ResetCodeStore
{
    private readonly Dictionary<string, (string Code, DateTime Expiry)> _resetCodes = new();

    public void StoreCode(string email, string code, TimeSpan expiry)
    {
        _resetCodes[email] = (code, DateTime.UtcNow.Add(expiry));
    }

    public bool ValidateCode(string email, string code, out bool isExpired)
    {
        isExpired = false;
        if (!_resetCodes.TryGetValue(email, out var stored))
        {
            return false;
        }

        if (stored.Expiry < DateTime.UtcNow)
        {
            isExpired = true;
            _resetCodes.Remove(email);
            return false;
        }

        return stored.Code == code;
    }

    public void RemoveCode(string email)
    {
        _resetCodes.Remove(email);
    }
}