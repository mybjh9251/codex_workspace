using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NuGetDependencyDownloaderWPF
{
    public class PackageModel : INotifyPropertyChanged
    {
        private string id = string.Empty;
        private string version = string.Empty;
        private string kind = "Root";
        private string status = "Pending";
        private string message = string.Empty;
        private int progress;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Id
        {
            get => id;
            set => SetField(ref id, value);
        }

        public string Version
        {
            get => version;
            set => SetField(ref version, value);
        }

        public string Kind
        {
            get => kind;
            set => SetField(ref kind, value);
        }

        public string Status
        {
            get => status;
            set => SetField(ref status, value);
        }

        public string Message
        {
            get => message;
            set => SetField(ref message, value);
        }

        public int Progress
        {
            get => progress;
            set => SetField(ref progress, value);
        }

        public string Key => PackageKey.Create(Id, Version);

        public PackageModel CloneAsRoot()
        {
            return new PackageModel
            {
                Id = Id,
                Version = Version,
                Kind = "Root",
                Status = "Pending",
                Message = string.Empty,
                Progress = 0
            };
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
