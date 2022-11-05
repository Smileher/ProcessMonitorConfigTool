using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessMonitorConfigTool
{
    internal class EditWindowVM : ObservableRecipient
    {
        public EditWindowVM(ProcessInfo selectedProcessInfo)
        {
            SelectedProcessInfo = selectedProcessInfo;
        }
        private ProcessInfo _selectedProcessInfo;
        public ProcessInfo SelectedProcessInfo
        {
            get => _selectedProcessInfo;
            set => SetProperty(ref _selectedProcessInfo, value);
        }

    }
}
