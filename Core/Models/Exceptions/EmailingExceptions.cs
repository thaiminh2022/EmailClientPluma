namespace EmailClientPluma.Core.Models.Exceptions;

internal class EmailFetchException : Exception
{
    public EmailFetchException(string msg = "Không thể lấy email từ máy chủ", Exception? inner = null) : base(msg, inner)
    {
    }
}

internal class EmailSendException : Exception
{
    public EmailSendException(string msg = "Không thể gửi email đến máy chủ", Exception? inner = null) : base(msg, inner)
    {
    }
}

internal class EmailTokenException : Exception
{
    public EmailTokenException(string msg = "Không thể lấy thông tin token từ máy chủ", Exception? inner = null) : base(msg, inner)
    {
    }
}