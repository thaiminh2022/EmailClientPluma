using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmailClientPluma.Core.Models
{
    internal interface IRequestClose
    {
        event EventHandler<bool?>? RequestClose;
    }
}
