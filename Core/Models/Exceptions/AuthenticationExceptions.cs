namespace EmailClientPluma.Core.Models.Exceptions;

internal class AuthForbiddenException : Exception
{
    public AuthForbiddenException(string msg = "Người dùng không được phép đăng nhập", Exception? inner = null) 
        : base(msg) { }

}

internal class AuthCancelException : Exception
{
    public AuthCancelException(string msg = "Người dùng hủy đăng nhập", Exception? inner = null) :
        base(msg) { }
}

internal class AuthRefreshException : Exception
{
    public AuthRefreshException(string msg = "Không thể lấy thông tin đăng nhập tự động, hãy đăng nhập lại", Exception? inner = null) 
        : base(msg) { }
}
internal class AuthFailedException : Exception
{
    public AuthFailedException(string msg = "Không thể lấy thông tin đăng nhập, hãy đăng nhập lại", Exception? inner = null)
        : base(msg) { }
}
