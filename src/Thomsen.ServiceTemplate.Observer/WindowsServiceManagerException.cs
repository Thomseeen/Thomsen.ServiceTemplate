using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Thomsen.ServiceTemplate.Observer;

[Serializable]
public class WindowsServiceManagerException : Exception {
    private readonly int _errorCode;

    public WindowsServiceManagerException(string message, int errorCode) : base(message) {
        _errorCode = errorCode;
    }

    public bool IsServiceNotInstalledError => _errorCode == 1060;
}
