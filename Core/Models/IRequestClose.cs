namespace EmailClientPluma.Core.Models
{
    internal interface IRequestClose
    {
        event EventHandler<bool?>? RequestClose;
    }
}
