namespace EmailClientPluma.Core.Models.Exceptions;

internal class EmailFetchException(string msg = "Không thể lấy email từ máy chủ", Exception? inner = null)
    : Exception(msg, inner);

internal class EmailSendException(string msg = "Không thể gửi email đến máy chủ", Exception? inner = null)
    : Exception(msg, inner);

internal class EmailTokenException(string msg = "Không thể lấy thông tin token từ máy chủ", Exception? inner = null)
    : Exception(msg, inner);