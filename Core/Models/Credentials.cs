namespace EmailClientPluma.Core.Models;

public record Credentials
{
    public Credentials(string sessionToken, string refreshToken)
    {
        SessionToken = sessionToken;
        RefreshToken = refreshToken;
    }

    public string SessionToken { get; set; }
    public string RefreshToken { get; set; }
}