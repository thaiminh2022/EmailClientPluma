namespace EmailClientPluma.Core.Models.Exceptions;


internal class ReadAccountException : Exception
{
    public ReadAccountException(
        string msg = "Không thể lấy thông tin từ tài khoản đã đăng nhập, kiểm tra quyền đọc của phần mềm",
        Exception? inner = null) : base(msg, inner)
    {
    }
}

internal class ReadEmailException : Exception
{
    public ReadEmailException(
        string msg = "Không thể lấy thông tin email, kiểm tra quyền đọc của phần mềm",
        Exception? inner = null) : base(msg, inner)
    {
    }
}

internal class WriteEmailException : Exception
{
    public WriteEmailException(
        string msg = "Không thể lưu thông tin email, kiểm tra quyền viết của phần mềm",
        Exception? inner = null) : base(msg, inner)
    {
    }
}

internal class WriteAccountException : Exception
{
    public WriteAccountException(
        string msg = "Không thể lưu thông tin từ tài khoản đã đăng nhập, kiểm tra quyền viết của phần mềm",
        Exception? inner = null) : base(msg, inner)
    {
    }
}

internal class RemoveAccountException : Exception
{
    public RemoveAccountException(
        string msg = "Không thể xóa hoàn toàn thông tin tài khoản",
        Exception? inner = null) : base(msg, inner)
    {
    }
}


internal class WriteLabelException : Exception
{
    public WriteLabelException(
        string msg = "Không thể lưu thông tin label, kiểm tra quyền viết của phần mềm",
        Exception? inner = null) : base(msg, inner)
    {
    }
}

internal class ReadLabelException : Exception
{
    public ReadLabelException(
        string msg = "Không thể đọc thông tin của các labels, kiểm tra quyền đọc của phần mềm",
        Exception? inner = null) : base(msg, inner)
    {
    }
}

internal class RemoveLabelException : Exception
{
    public RemoveLabelException(
        string msg = "Không thể xóa hoàn toàn thông tin của dán nhãn",
        Exception? inner = null) : base(msg, inner)
    {
    }
}