namespace EmailClientPluma.Core.Models
{
    public record Credentials
    {
        public string SessionToken { get; set; }
        public string RefreshToken { get; set; }


        public Credentials(string sessionToken, string refreshToken)
        {
            SessionToken = sessionToken;
            RefreshToken = refreshToken;

        }


    }
}
