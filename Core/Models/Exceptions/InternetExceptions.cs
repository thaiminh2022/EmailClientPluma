namespace EmailClientPluma.Core.Models.Exceptions;

internal class NoInternetException(string msg = "Không có kết nối mạng, kiểm tra internet.", Exception? inner = null)
    : Exception(msg, inner);