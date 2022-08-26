using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Thomsen.ServiceTemplate.Observer;

internal class CustomCursor : IDisposable {
    private readonly Cursor _savedCursor;

    public CustomCursor(Cursor newCursor) {
        _savedCursor = Mouse.OverrideCursor;
        Mouse.OverrideCursor = newCursor;
    }

    public void Dispose() {
        Mouse.OverrideCursor = _savedCursor;
        GC.SuppressFinalize(this);
    }
}

internal class WaitCursor : CustomCursor {
    public WaitCursor() : base(Cursors.Wait) { }
}
