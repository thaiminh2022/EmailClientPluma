namespace EmailClientPluma.Core.Models.Exceptions;


internal class ReadAccountException(
    string msg = "Không thể lấy thông tin từ tài khoản đã đăng nhập, kiểm tra quyền đọc của phần mềm",
    Exception? inner = null)
    : Exception(msg, inner);

internal class ReadEmailException(
    string msg = "Không thể lấy thông tin email, kiểm tra quyền đọc của phần mềm",
    Exception? inner = null)
    : Exception(msg, inner);

internal class WriteEmailException(
    string msg = "Không thể lưu thông tin email, kiểm tra quyền viết của phần mềm",
    Exception? inner = null)
    : Exception(msg, inner);

internal class WriteAccountException(
    string msg = "Không thể lưu thông tin từ tài khoản đã đăng nhập, kiểm tra quyền viết của phần mềm",
    Exception? inner = null)
    : Exception(msg, inner);

internal class RemoveAccountException(
    string msg = "Không thể xóa hoàn toàn thông tin tài khoản",
    Exception? inner = null)
    : Exception(msg, inner);


internal class WriteLabelException(
    string msg = "Không thể lưu thông tin label, kiểm tra quyền viết của phần mềm",
    Exception? inner = null)
    : Exception(msg, inner);

internal class ReadLabelException(
    string msg = "Không thể đọc thông tin của các labels, kiểm tra quyền đọc của phần mềm",
    Exception? inner = null)
    : Exception(msg, inner);

internal class RemoveLabelException(
    string msg = "Không thể xóa hoàn toàn thông tin của dán nhãn",
    Exception? inner = null)
    : Exception(msg, inner);