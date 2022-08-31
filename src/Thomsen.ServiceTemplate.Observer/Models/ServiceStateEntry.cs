using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Thomsen.ServiceTemplate.Observer.Models {
    internal record ServiceStateEntry : BaseModel {
        private ServiceState _state;

        public ServiceObserverSettings Settings { get; init; } = default!;

        public ServiceState State { get => _state; set => SetProperty(ref _state, value); }
    }
}
