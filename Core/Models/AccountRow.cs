namespace EmailClientPluma.Core.Services
{
    internal partial class StorageService
    {
        private sealed class AccountRow
        {
            public string PROVIDER_UID { get; set; } = "";
            public string PROVIDER { get; set; } = "";
            public string EMAIL { get; set; } = "";
            public string DISPLAY_NAME { get; set; } = "";
        }
    }


}
