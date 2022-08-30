using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Thomsen.ServiceTemplate.Observer;

internal class Program {
    [STAThread]
    public static void Main(string[] args) {
        ObserverApp application = new();
        application.InitializeComponent();
        application.Run();
    }
}
