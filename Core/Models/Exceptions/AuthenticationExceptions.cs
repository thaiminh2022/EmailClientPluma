namespace EmailClientPluma.Core.Models.Exceptions;

internal class AuthForbiddenException(string msg = "Người dùng không được phép đăng nhập", Exception? inner = null)
    : Exception(msg, inner);

internal class AuthCancelException(string msg = "Người dùng hủy đăng nhập", Exception? inner = null)
    : Exception(msg, inner);

internal class AuthRefreshException(
    string msg = "Không thể lấy thông tin đăng nhập tự động, hãy đăng nhập lại",
    Exception? inner = null)
    : Exception(msg, inner);
internal class AuthFailedException(
    string msg = "Không thể lấy thông tin đăng nhập, hãy đăng nhập lại",
    Exception? inner = null)
    : Exception(msg, inner);
